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
                    string metadataFileName = Path.ChangeExtension(filename, ".metadata2.bin");
                    // Call your SaveMetadata method passing the metadata file name, the rawFile, and scan numbers.
                    MetadataSerializer.SaveMetadata(metadataFileName, rawFile, firstScanNumber, lastScanNumber);
                    Console.WriteLine("Metadata binary file saved: " + metadataFileName);
                    
                }
                // ------------------------------------------


                // Get a spectrum from the RAW file.  
                if (writeSpectrum)
                {
                    // Create a binary file based on raw file name
                    //string binFileName = Path.ChangeExtension(filename, ".bin");
                   
                    string binFileName = "test_binary.bin";

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
                //writer.Write(firstScan);
                //writer.Write(lastScan);
                //writer.Write(startTime);
                //writer.Write(endTime);

                // Write the total number of scans.
                int numScans = lastScan - firstScan + 1;
                //writer.Write(numScans);

                // Loop over every scan.
                for (int scanNumber = firstScan; scanNumber <= lastScan; scanNumber++)
                {
                    Console.WriteLine("get all spectrum");
                    //CentroidScan(rawFile, scanNumber);
                    GetSpectrum(rawFile, scanNumber, writer);
                    
                }
            }
        }

        // Method to centroid a scan
        private static void CentroidScan(IRawDataPlus rawFile, int scanNumber)
        {
            // Get the scan from the RAW file
            var scan = Scan.FromFile(rawFile, scanNumber);

            // Centroid the scan
            var centroidedScan = Scan.ToCentroid(scan);

            Console.WriteLine("test centroid for scan # {}", scanNumber);
            /*
            // Reflection to find object's properties and methods
            Type type = centroidedScan.GetType();
            // List properties
            Console.WriteLine("Properties:");
            foreach (PropertyInfo prop in type.GetProperties())
                Console.WriteLine(prop.Name);
            Console.WriteLine("Methods:");
            // List methods
            foreach (MethodInfo method in type.GetMethods())
                Console.WriteLine(method.Name);
            
            var masses = centroidedScan.PreferredMasses;
            var intensities = centroidedScan.PreferredIntensities;

            for (int i=0; i< masses.Length; i++)
            {
                Console.WriteLine("  {0:F4} {1:F0}", masses[i], intensities[i]);
            }
            */

            // See if the returned scan object has a centroid stream
            if (centroidedScan.HasCentroidStream)
            {
                Console.WriteLine("returned scan has centroid stream"); //never executed
                Console.WriteLine("Average spectrum ({0} points)", centroidedScan.CentroidScan.Length);

                // Print the spectral data (mass, intensity values)
                for (int i = 0; i < centroidedScan.CentroidScan.Length; i++)
                {
                    Console.WriteLine("  {0:F4} {1:F0}", centroidedScan.CentroidScan.Masses[i], centroidedScan.CentroidScan.Intensities[i]);
                }
            
            }
        }

        // Method to retrieve spectrum data for a single scan
        // we first write the scan number and then a “data type” flag (1 for centroid scans, 0 for profile scans)
        private static void GetSpectrum(IRawDataPlus rawFile, int scanNumber, BinaryWriter writer)
        {
            // Get the scan statistics from the RAW file for this scan number
            var scanStatistics = rawFile.GetScanStatsForScanNumber(scanNumber);

            // Write the scan number.
            writer.Write(scanNumber);

            // Check to see if the scan has centroid data or profile data.

            if (scanStatistics.IsCentroidScan && scanStatistics.SpectrumPacketType == SpectrumPacketType.FtCentroid)
            {
                writer.Write((byte)1);

                // Get the centroid (label) data from the RAW file for this scan
                var centroidStream = rawFile.GetCentroidStream(scanNumber, false);
                int numPoints = centroidStream.Length;
                writer.Write(numPoints);
                Console.WriteLine("Spectrum (centroid/label) {0} - {1} points", scanNumber, centroidStream.Length);

                // Print the spectral data (mass, intensity, charge values).  Not all of the information in the high resolution centroid 
                // (label data) object is reported in this example.  Please check the documentation for more information about what is
                // available in high resolution centroid (label) data.
                
                for (int i = 0; i < numPoints; i++)
                {
                    writer.Write(centroidStream.Masses[i]);      // double
                    writer.Write(centroidStream.Intensities[i]);   // double
                    writer.Write(centroidStream.Charges[i]);       // int
                    //Console.WriteLine("  {0} - {1:F4}, {2:F0}, {3:F0}", i, centroidStream.Masses[i], centroidStream.Intensities[i], centroidStream.Charges[i]);
                }
                
            }
            else
            {
                writer.Write((byte)0);

                // Get the segmented (low res and profile) scan data
                
                var segmentedScan = rawFile.GetSegmentedScanFromScanNumber(scanNumber, scanStatistics);
                int numPoints = segmentedScan.Positions.Length;
                writer.Write(numPoints);
                Console.WriteLine("Spectrum (profile data) {0} - {1} points", scanNumber, segmentedScan.Positions.Length);

                // Print the spectral data (mass, intensity values) 
                for (int i = 0; i < numPoints; i++)
                {
                    writer.Write(segmentedScan.Positions[i]);    // double
                    writer.Write(segmentedScan.Intensities[i]);    // double
                    if (i < 10){
                    Console.WriteLine("  {0} - {1:F4}, {2:F0}", i, segmentedScan.Positions[i], segmentedScan.Intensities[i]);
                    }
                }
                
            }
        }
    }
}