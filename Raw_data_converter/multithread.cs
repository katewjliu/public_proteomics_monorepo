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
        string binFileName = "test_binary_profile_0430_multithreading_3.bin";

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
                                GetSpectrum(threadDataAccessor, scanNumber, writer);
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

    private static void GetSpectrum(IRawDataPlus rawFile, int scanNumber, BinaryWriter writer)
    {
        if (rawFile == null)
        {
            Console.Error.WriteLine("Error: rawFile is null.");
            return;
        }

        Console.WriteLine($"Processing spectrum for scan number: {scanNumber}");

        // Get the scan statistics from the RAW file for this scan number
        var scanStatistics = rawFile.GetScanStatsForScanNumber(scanNumber);
        
        if (scanStatistics == null)
        {
            Console.Error.WriteLine($"Error: Scan statistics for scan number {scanNumber} is null.");
            return;
        }

        // Write the scan number
        writer.Write(scanNumber);

        // Check to see if the scan has centroid data or profile data.
        if (scanStatistics.IsCentroidScan && scanStatistics.SpectrumPacketType == SpectrumPacketType.FtCentroid)
        {
            writer.Write((byte)1); // Indicate centroid data

            // Get the centroid (label) data from the RAW file for this scan
            var centroidStream = rawFile.GetCentroidStream(scanNumber, false);
            int numPoints = centroidStream.Length;
            writer.Write(numPoints);
            Console.WriteLine("Spectrum (centroid/label) {0} - {1} points", scanNumber, centroidStream.Length);

            // Write centroid data (mass, intensity, charge values)
            for (int i = 0; i < numPoints; i++)
            {
                writer.Write(centroidStream.Masses[i]);      // double
                writer.Write(centroidStream.Intensities[i]);   // double
                writer.Write(centroidStream.Charges[i]);       // int
            }
        }
        else
        {
            writer.Write((byte)0); // Indicate profile data

            // Get the segmented (low res and profile) scan data
            var segmentedScan = rawFile.GetSegmentedScanFromScanNumber(scanNumber, scanStatistics);
            int numPoints = segmentedScan.Positions.Length;
            writer.Write(numPoints);
            Console.WriteLine("Spectrum (profile data) {0} - {1} points", scanNumber, segmentedScan.Positions.Length);

            // Write profile data (mass, intensity values)
            for (int i = 0; i < numPoints; i++)
            {
                writer.Write(segmentedScan.Positions[i]);    // double
                writer.Write(segmentedScan.Intensities[i]);    // double
            }
        }
    }
}