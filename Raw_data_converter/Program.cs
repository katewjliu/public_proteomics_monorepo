using System;
using System.IO;
using System.Reflection;

using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.MassPrecisionEstimator;
using ThermoFisher.CommonCore.RawFileReader;
namespace RawFileConverter
{
    public class SingleThread
    {
        private static void Main(string[] args)
        {
            bool writeMetadata = false;
            bool writeSpectrum = true;

            try
            { 
                // Check to see if the RAW file name was supplied as an argument to the program
                string filename = string.Empty;
                if (args.Length > 0)
                {
                    filename = args[0];
                }
                if (string.IsNullOrEmpty(filename))
                {
                    Console.WriteLine("No RAW file specified!");
                    return;
                }
                // Check to see if the specified RAW file exists
                if (!File.Exists(filename))
                {
                    Console.WriteLine(@"The file doesn't exist in the specified location - " + filename);
                    return;
                }
                // Create the IRawDataPlus object for accessing the RAW file
                var rawFile = RawFileReaderAdapter.FileFactory(filename);
                if (!rawFile.IsOpen)
                {
                    Console.WriteLine("Unable to access the RAW file using the RawFileReader class!");  
                    return;
                }
                // Check for any errors in the RAW file
                if (rawFile.IsError)
                {
                    Console.WriteLine("Error opening ({0}) - {1}", rawFile.FileError, filename);
                    return;
                }
                // Check if the RAW file is being acquired
                if (rawFile.InAcquisition)
                {
                    Console.WriteLine("RAW file still being acquired - " + filename);
                    return;
                }


                // Get the number of instruments (controllers) present in the RAW file and set the 
                // selected instrument to the MS instrument, first instance of it
                Console.WriteLine("The RAW file has data from {0} instruments", rawFile.InstrumentCount);

                rawFile.SelectInstrument(Device.MS, 1); // what's first instance of MS instrument? 

                // Get the first and last scan from the RAW file
                int firstScanNumber = rawFile.RunHeaderEx.FirstSpectrum;
                int lastScanNumber = rawFile.RunHeaderEx.LastSpectrum;

                // Get the start and end time from the RAW file
                double startTime = rawFile.RunHeaderEx.StartTime;
                double endTime = rawFile.RunHeaderEx.EndTime;
                
                // Pring some OS information
                Console.WriteLine("General File Information:");
                Console.WriteLine("   RAW file: " + rawFile.FileName);
                Console.WriteLine("   Scan range: {0} - {1}", firstScanNumber, lastScanNumber);
                Console.WriteLine("   Time range: {0:F2} - {1:F2}", startTime, endTime);
                
                // --------- Call SaveMetadata Here ---------
                if (writeMetadata)
                {
                    // Create a filename for the metadata binary file (e.g., with a .metadata.bin extension)
                    string metadataFileName = Path.ChangeExtension(filename, ".metadata.bin");
                    // Call your SaveMetadata method passing the metadata file name, the rawFile, and scan numbers.
                    MetadataSerializer.SaveMetadata(metadataFileName, rawFile, firstScanNumber, lastScanNumber);
                    Console.WriteLine("Metadata binary file saved: " + metadataFileName);
                    
                }
                // ------------------------------------------


                // Get a spectrum from the RAW file.  
                if (writeSpectrum)
                {
                    // Create a binary file based on raw file name
                    string binFileName = Path.ChangeExtension(filename, ".bin");

                    GetAllSpectra(rawFile, binFileName);
                    Console.WriteLine("Spectrum data stored in binary file: " + binFileName + " (" + binFileName.Length + " bytes)");
                    
                }

                // Close (dispose) the RAW file
                
                Console.WriteLine("Closing " + filename);
                rawFile.Dispose();            

            }
            catch(Exception ex)
            {
                Console.WriteLine("Error accessing RAWFileReader library! - " + ex.Message);
            }
        }

        // Method to iterate over all scan numbers and retrieve their spectra
        private static void GetAllSpectra(IRawDataPlus rawFile, string binFileName)
        {
            // Open the binary file for writing.
            using (BinaryWriter writer = new BinaryWriter(File.Open(binFileName, FileMode.Create)))
            {
                // (Optional) Write some global header information.
                int firstScan = rawFile.RunHeaderEx.FirstSpectrum;
                int lastScan = rawFile.RunHeaderEx.LastSpectrum;
                double startTime = rawFile.RunHeaderEx.StartTime;
                double endTime = rawFile.RunHeaderEx.EndTime;

                // Write the total number of scans.
                int numScans = lastScan - firstScan + 1;
                writer.Write(numScans);

                // Loop over every scan.
                for (int scanNumber = firstScan; scanNumber <= lastScan; scanNumber++)
                {
                    try
                    {
                        // write scan # in binary file 
                        writer.Write(scanNumber);

                        // Get the scan filter for the spectrum
                        var scanFilter = rawFile.GetFilterForScanNumber(scanNumber);

                        if (string.IsNullOrEmpty(scanFilter.ToString()))
                        {
                            continue;
                        }

                        var scan = Scan.FromFile(rawFile, scanNumber);
                        
                        // If that scan contains FTMS data then Centroid stream will be populated so check to see if it is present.
                        int labelSize = 0;

                        if (scan.HasCentroidStream)
                        {
                            
                            labelSize = scan.CentroidScan.Length;
                            writer.Write(labelSize);
                        }

                        for (int i=0; i<labelSize; i++)
                        {
                            //Console.WriteLine("Spectrum " + i + ": "+ scan.CentroidScan.Masses[i]+ ", "+ scan.CentroidScan.Intensities[i]);
                            // write to bin
                            writer.Write(scan.CentroidScan.Masses[i]);      // double
                            writer.Write(scan.CentroidScan.Intensities[i]);   // double
                           

                        }

                        // For non-FTMS data, the preferred data will be populated
                        int dataSize = scan.PreferredMasses.Length;
                        Console.WriteLine("Spectrum {0} - {1}: normal {2}, label {3} points", scanNumber, scanFilter.ToString(), dataSize, labelSize);
                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error reading spectrum {0} - {1}", scanNumber, ex.Message);
                    }
                    
                }
            }
        }

        
    }
}