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

        // 2) Decide how many parallel accessors you want
        int threadCount = Environment.ProcessorCount;
        string tmpDir = Path.GetTempPath();
        var tempFiles = Enumerable.Range(0, threadCount)
                          .Select(i => Path.Combine(tmpDir, $"temp_{i}.bin"))
                          .ToArray();

        // 3) Parallel.For to spin up that many accessors
        Parallel.For(0, threadCount, threadIndex =>
        {
            using (var writer = new BinaryWriter(File.Open(tempFiles[threadIndex], FileMode.Create, FileAccess.Write)))
            using (IRawDataPlus accessor = threadManager.CreateThreadAccessor())
                    
            {
                accessor.SelectInstrument(Device.MS, 1);

                int scansPerThread = numScans / threadCount;
                int remainder      = numScans % threadCount;
                int startScan      = firstScanNumber + threadIndex * scansPerThread;
                int endScan        = startScan + scansPerThread - 1;
                if (threadIndex == threadCount - 1)
                    endScan += remainder;

                // **No locking here**—each thread writes to its own file
                for (int scan = startScan; scan <= endScan; scan++)
                {
                    ReadSpectrum(accessor, scan, writer);
                }
            }
        });
             
        // ### 3) Merge temp files into final output ###
        string finalBin = Path.ChangeExtension(args[0], "multithread_splitmerge.bin");
        using (var outFs = File.Open(finalBin, FileMode.Create, FileAccess.Write))
        using (var outWriter = new BinaryWriter(outFs))
        {
            // Write total scan count once
            outWriter.Write(numScans);

            // Append each temp file’s raw bytes
            foreach (var part in tempFiles)
            {
                using (var inFs = File.OpenRead(part))
                {
                    inFs.CopyTo(outFs);
                }
                File.Delete(part);
            }
        }

        threadManager.Dispose();
        Console.WriteLine($"Done: wrote {numScans} scans to {finalBin}");
    }
    private static void ReadSpectrum(IRawDataPlus rawFile, int scanNumber, BinaryWriter writer)
    {
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