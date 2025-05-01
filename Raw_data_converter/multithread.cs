using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.RawFileReader;

public class RawDataMultiThreadedProcessor
{
    /// <summary>
    /// Opens the specified raw file for parallel access,
    /// spinning up one accessor per logical thread to process data in parallel.
    /// </summary>
    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: RawDataMultiThreadedProcessor <rawFilePath>");
            return;
        }

        // 1) Create the thread manager (single time, single-threaded open)
        IRawFileThreadManager threadManager = null!;
        try
        {
            threadManager = RawFileReaderFactory.CreateThreadManager(args[0]);
        }
        catch (ArgumentNullException ane)
        {
            Console.Error.WriteLine($"Filename was null: {ane.Message}");
            return;
        }
        catch (DllNotFoundException dnfe)
        {
            Console.Error.WriteLine($"Raw reader DLL not found: {dnfe.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error opening file: {ex.Message}");
            return;
        }

        // Open the file once to get general metadata (RunHeaderEx) info
        IRawDataPlus dataAccessor = threadManager.CreateThreadAccessor();
        dataAccessor.SelectInstrument(Device.MS, 1);
        int firstScanNumber = dataAccessor.RunHeaderEx.FirstSpectrum;
        int lastScanNumber = dataAccessor.RunHeaderEx.LastSpectrum;
        double startTime = dataAccessor.RunHeaderEx.StartTime;
        double endTime = dataAccessor.RunHeaderEx.EndTime;

        // Print general file information
        Console.WriteLine("General File Information:");
        Console.WriteLine("   RAW file: " + dataAccessor.FileName);
        Console.WriteLine("   Scan range: {0} - {1}", firstScanNumber, lastScanNumber);
        Console.WriteLine("   Time range: {0:F2} - {1:F2}", startTime, endTime);

        // Create binary file for writing spectrum
        string binFileName = Path.ChangeExtension(args[0], "multithread.bin");

        // 2) Decide how many parallel accessors you want
        int threadCount = Environment.ProcessorCount;

        // Use a lock to make sure only one thread writes to the file at a time
        object writeLock = new object();

        try
        {
            // Open the BinaryWriter once outside the Parallel.For loop
            using (BinaryWriter writer = new BinaryWriter(File.Open(binFileName, FileMode.Create)))
            {
                // Write total num of scans into binary file first 
                int numScans = lastScanNumber - firstScanNumber + 1;
                writer.Write(numScans);

                // 3) Parallel.For to spin up that many accessors
                Parallel.For(0, threadCount, threadIndex =>
                {
                    // Each thread gets its own IRawDataPlus instance
                    using (IRawDataPlus threadDataAccessor = threadManager.CreateThreadAccessor())
                    {   
                        // select device for each thread
                        threadDataAccessor.SelectInstrument(Device.MS, 1);
                        // Check for any file-level errors
                        if (threadDataAccessor.FileError != null && threadDataAccessor.FileError.ErrorCode != 0)
                        {
                            Console.Error.WriteLine(
                                $"Thread {threadIndex}: file error {threadDataAccessor.FileError.ErrorCode} - " +
                                threadDataAccessor.FileError.ErrorMessage);
                            return;
                        }

                        // Distribute scans across threads
                        int totalScans = lastScanNumber - firstScanNumber + 1;
                        int scansPerThread = totalScans / threadCount;
                        int remainder = totalScans % threadCount; // In case the scans aren't evenly divisible
                        int threadStartScan = firstScanNumber + threadIndex * scansPerThread;
                        int threadEndScan = threadStartScan + scansPerThread - 1;

                        if (threadIndex == threadCount - 1) // Last thread, take any remaining scans
                        {
                            threadEndScan += remainder;
                        }

                        // Process each scan within the range
                        for (int scanNumber = threadStartScan; scanNumber <= threadEndScan; scanNumber++)
                        {
                            // Lock to ensure only one thread writes to the file at a time
                            lock (writeLock)
                            {
                                ReadSpectrum(threadDataAccessor, scanNumber, writer);
                            }
                        }
                    } // disposing threadDataAccessor closes that thread's handle
                });
            } // BinaryWriter is disposed after the Parallel.For block
        }
        finally
        {
            // 4) Once all threads finish, dispose the manager
            threadManager.Dispose();
        }
    }

    private static void ReadSpectrum(IRawDataPlus rawFile, int scanNumber, BinaryWriter writer)
    {
        if (rawFile == null)
        {
            Console.Error.WriteLine("Error: rawFile is null.");
            return;
        }

        try
        {
            // write scan # in binary file 
            writer.Write(scanNumber);

            // Get the scan filter for the spectrum
            var scanFilter = rawFile.GetFilterForScanNumber(scanNumber);

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