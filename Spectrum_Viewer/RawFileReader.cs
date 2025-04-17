sing System;
using System.IO;
 // Make sure you have the proper references
using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.MassPrecisionEstimator;
using ThermoFisher.CommonCore.RawFileReader;

namespace RawFileReaderLib
{
    public class ScanRange
    {
        public int FirstScanNumber { get; set; }
        public int LastScanNumber { get; set; }
    }

    public class RawFileReaderService
    {   
        // Store the raw file as a class-level variable so it can be reused by multiple methods.
        private IRawDataPlus? rawFile;
        
        // Open raw file 
        public void OpenRawFile(string rawFilePath)
        {
            if (string.IsNullOrWhiteSpace(rawFilePath) || !File.Exists(rawFilePath))
                throw new ArgumentException("Invalid raw file path.", nameof(rawFilePath));

            // Create the RawFileReader object.
            rawFile = RawFileReaderAdapter.FileFactory(rawFilePath);

            if (!rawFile.IsOpen)
                throw new Exception("Unable to open the raw file.");

            if (rawFile.IsError)
                throw new Exception($"Error opening raw file: {rawFile.FileError}");

            // Optionally, check if the file is still being acquired
            if (rawFile.InAcquisition)
                throw new Exception("Raw file is still being acquired.");

            // Select the first MS instrument (or adjust as needed)
            rawFile.SelectInstrument(Device.MS, 1);
        }
        // Get Scan Ranges
        public ScanRange GetScanRange(string rawFilePath)
        {
            OpenRawFile(rawFilePath);

            // Retrieve the first and last scan numbers
            int firstScanNumber = rawFile!.RunHeaderEx.FirstSpectrum;
            int lastScanNumber = rawFile.RunHeaderEx.LastSpectrum;

            return new ScanRange
            {
                FirstScanNumber = firstScanNumber,
                LastScanNumber = lastScanNumber
            };
        }

        // Get Scan Filter to display on Spectrum
        public string GetScanDetails(int scan)
        {
            if (rawFile == null)
                throw new InvalidOperationException("Raw file not opened. ");

            double time = rawFile.RetentionTimeFromScanNumber(scan);
            var scanFilter = rawFile.GetFilterForScanNumber(scan);
            var scanEvent = rawFile.GetScanEventForScanNumber(scan);

            // For MS2 scans:
            if (scanFilter.MSOrder == MSOrderType.Ms2)
            {
                // Get the reaction information for the first precursor
                var reaction = scanEvent.GetReaction(0);

                double precursorMass = reaction.PrecursorMass;
                double collisionEnergy = reaction.CollisionEnergy;
                double isolationWidth = reaction.IsolationWidth;
                double monoisotopicMass = 0.0;
                int masterScan = 0;
                var ionizationMode = scanFilter.IonizationMode;
                var order = scanFilter.MSOrder;

                // Retrieve the trailer extra data and look for specific labels
                var trailerData = rawFile.GetTrailerExtraInformation(scan);
                for (int i = 0; i < trailerData.Length; i++)
                {
                    if (trailerData.Labels[i] == "Monoisotopic M/Z:")
                    {
                        monoisotopicMass = Convert.ToDouble(trailerData.Values[i]);
                    }
                    if (trailerData.Labels[i] == "Master Scan Number:" ||
                        trailerData.Labels[i] == "Master Scan Number" ||
                        trailerData.Labels[i] == "Master Index:")
                    {
                        masterScan = Convert.ToInt32(trailerData.Values[i]);
                    }
                }
               
                // Build and return the formatted string for an MS2 scan
                return string.Format(
                    "Scan number {0} @ time {1:F2} - Master scan = {2}, Ionization mode = {3}, MS Order = {4}, Precursor mass = {5:F4}, Monoisotopic Mass = {6:F4}, Collision energy = {7:F2}, Isolation width = {8:F2}",
                    scan, time, masterScan, ionizationMode, order, precursorMass, monoisotopicMass, collisionEnergy, isolationWidth);
            }
            // For MS1 scans:
            else if (scanFilter.MSOrder == MSOrderType.Ms)
            {
                var scanDependents = rawFile.GetScanDependents(scan, 5);

                if (scanDependents != null)
                {
                    return string.Format(
                        "Scan number {0} @ time {1:F2} - Instrument type = {2}, Number of dependent scans = {3}",
                        scan, time, scanDependents.RawFileInstrumentType, scanDependents.ScanDependentDetailArray.Length);
                }
                else
                {
                    return string.Format("Scan number {0} @ time {1:F2} - No dependent scans found.", scan, time);
                }
            }
            else
            {
                return string.Format("Scan number {0} @ time {1:F2} - Unknown MS Order.", scan, time);
            }
        }

        // return spectrum data for this scan
        public Dictionary<string, object> GetSpectrum(int scanNumber)
        {
            if (rawFile == null)
                throw new InvalidOperationException("Raw file not opened. Call GetScanInfo first.");

            var spectrumData = new Dictionary<string, object>
            {
                { "ScanNumber", scanNumber }
            };

            var scanStatistics = rawFile.GetScanStatsForScanNumber(scanNumber);

            if (scanStatistics.IsCentroidScan && scanStatistics.SpectrumPacketType == SpectrumPacketType.FtCentroid)
            {
                var centroidStream = rawFile.GetCentroidStream(scanNumber, false);
                var centroidData = new List<Dictionary<string, object>>();

                for (int i = 0; i < centroidStream.Length; i++)
                {
                    centroidData.Add(new Dictionary<string, object>
                    {
                        { "Mass", centroidStream.Masses[i] },
                        { "Intensity", centroidStream.Intensities[i] },
                        { "Charge", centroidStream.Charges[i] }
                    });
                }

                spectrumData["CentroidData"] = centroidData;
            }
            else
            {
                var segmentedScan = rawFile.GetSegmentedScanFromScanNumber(scanNumber, scanStatistics);
                var segmentedData = new List<Dictionary<string, object>>();

                for (int i = 0; i < segmentedScan.Positions.Length; i++)
                {
                    segmentedData.Add(new Dictionary<string, object>
                    {
                        { "Mass", segmentedScan.Positions[i] },
                        { "Intensity", segmentedScan.Intensities[i] }
                    });
                }

                spectrumData["SegmentedData"] = segmentedData;
            }

            return spectrumData;
        }

    }
}
