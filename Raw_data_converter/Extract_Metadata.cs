using System;
using System.Collections.Generic;
using System.IO;
using MyProject.Proto;  // Namespace from the generated .cs file
using Google.Protobuf;

using ThermoFisher.CommonCore.Data;
using ThermoFisher.CommonCore.Data.Business;
using ThermoFisher.CommonCore.Data.FilterEnums;
using ThermoFisher.CommonCore.Data.Interfaces;
using ThermoFisher.CommonCore.MassPrecisionEstimator;
using ThermoFisher.CommonCore.RawFileReader;


public class MetadataSerializer
{
    public static void SaveMetadata(string outputFile, IRawDataPlus rawFile, int firstScanNumber, int lastScanNumber)
    {
        // Create and populate the Metadata protobuf message
        Metadata metadata = new Metadata
        {
            GeneralFileInformation = new GeneralFileInformation
            {
                RawFileName = rawFile.FileName,
                RawFileVersion = rawFile.FileHeader.Revision.ToString(),
                CreationDate = rawFile.FileHeader.CreationDate.ToString(),
                OperatorName = rawFile.FileHeader.WhoCreatedId,
                NumberOfInstruments = rawFile.InstrumentCount,
                Description = rawFile.FileHeader.FileDescription,
                InstrumentModel = rawFile.GetInstrumentData().Model,
                InstrumentName = rawFile.GetInstrumentData().Name,
                SerialNumber = rawFile.GetInstrumentData().SerialNumber,
                SoftwareVersion = rawFile.GetInstrumentData().SoftwareVersion,
                FirmwareVersion = rawFile.GetInstrumentData().HardwareVersion,
                Units = rawFile.GetInstrumentData().Units.ToString(),
                MassResolution = rawFile.RunHeaderEx.MassResolution,
                NumberOfScans = rawFile.RunHeaderEx.SpectraCount,
                FirstScan = rawFile.RunHeaderEx.FirstSpectrum,
                LastScan = rawFile.RunHeaderEx.LastSpectrum,
                StartTime = rawFile.RunHeaderEx.StartTime.ToString(),
                EndTime = rawFile.RunHeaderEx.EndTime.ToString(),
                LowMass = rawFile.RunHeaderEx.LowMass,
                HighMass = rawFile.RunHeaderEx.HighMass
            },
            SampleInformation = new SampleInformationData
            {
                SampleName = rawFile.SampleInformation.SampleName,
                SampleId = rawFile.SampleInformation.SampleId,
                SampleType = rawFile.SampleInformation.SampleType.ToString(),
                Comment = rawFile.SampleInformation.Comment,
                Vial = rawFile.SampleInformation.Vial,
                SampleVolume = rawFile.SampleInformation.SampleVolume,
                InjectionVolume = rawFile.SampleInformation.InjectionVolume,
                RowNumber = rawFile.SampleInformation.RowNumber,
                DilutionFactor = rawFile.SampleInformation.DilutionFactor
            },
            // Assuming your proto defines TrailerExtraFields as a repeated string
            TrailerExtraFields = { rawFile.GetTrailerExtraHeaderInformation().Select(field => field.Label) },
            NumberOfFilters = rawFile.GetFilters().Count,
            FirstScanFilter = new ScanFilterData
            {
                FilterText = rawFile.GetFilterForScanNumber(firstScanNumber).ToString()
            },
            LastScanFilter = new ScanFilterData
            {
                FilterText = rawFile.GetFilterForScanNumber(lastScanNumber).ToString()
            }
        };
        
        // Loop over each scan to add scan-level metadata.
        // ReadScanInformation
        for (int scanNumber = firstScanNumber; scanNumber <= lastScanNumber; scanNumber++)
        {
            // Retrieve scan-specific information. Adjust these calls to match the Thermo Reader.
            
            double retentionTime = rawFile.RetentionTimeFromScanNumber(scanNumber);
            var scanFilter = rawFile.GetFilterForScanNumber(scanNumber);
            var scanEvent = rawFile.GetScanEventForScanNumber(scanNumber);
            
            // Create a ScanMetadata message for this scan.
            ScanMetadata scanMetadata = new ScanMetadata
            {
                ScanNumber = scanNumber,
                RetentionTime = retentionTime
            };

            // Check if scan is MS1
            if (scanFilter.MSOrder == MSOrderType.Ms) 
            {
                var scanDependents = rawFile.GetScanDependents(scanNumber, 5);
                if (scanDependents != null)
                {
                    scanMetadata.Ms1 = new MS1Metadata
                    {
                        RawFileInstrumentType = scanDependents.RawFileInstrumentType.ToString(),
                        NumDependentScans = scanDependents.ScanDependentDetailArray.Length
                    };
                }
                
            }
            // Check if scan is MS2
            else if (scanFilter.MSOrder == MSOrderType.Ms2)
            {
                // Get the reaction information for the first precursor.
                var reaction = scanEvent.GetReaction(0);

                // Initialize default values.
                double monoisotopicMass = 0.0;
                int masterScan = 0;

                // Get trailer extra information.
                var trailerData = rawFile.GetTrailerExtraInformation(scanNumber);
                for (int i = 0; i < trailerData.Length; i++)
                {
                    if (trailerData.Labels[i] == "Monoisotopic M/Z:")
                    {
                        monoisotopicMass = Convert.ToDouble(trailerData.Values[i]);
                    }
                    if ((trailerData.Labels[i] == "Master Scan Number:") ||
                        (trailerData.Labels[i] == "Master Scan Number") ||
                        (trailerData.Labels[i] == "Master Index:"))
                    {
                        masterScan = Convert.ToInt32(trailerData.Values[i]);
                    }
                }

                // Now use the computed values in the object initializer.
                scanMetadata.Ms2 = new MS2Metadata
                {
                    MasterScan = masterScan,
                    IonizationMode = scanFilter.IonizationMode.ToString(),
                    Order = scanFilter.MSOrder.ToString(),
                    PrecursorMass = reaction.PrecursorMass,
                    MonoisotopicMass = monoisotopicMass,
                    CollisionEnergy = reaction.CollisionEnergy,
                    IsolationWidth = reaction.IsolationWidth
                };
            }
            
            // else {}
            // optionally hangle cases for MS3 for example


            // Add the scan metadata to the repeated field.
            metadata.Scans.Add(scanMetadata);
        }

        // Serialize the metadata to a binary file.
        using (FileStream fs = new FileStream(outputFile, FileMode.Create))
        {
            // Create a CodedOutputStream from the FileStream.
            var codedOutput = new Google.Protobuf.CodedOutputStream(fs);
                
            metadata.WriteTo(fs);

            // Flush to ensure all data is written.
            codedOutput.Flush();
            
        }
        // JSON for debugging
        string json = JsonFormatter.Default.Format(metadata);
        Console.WriteLine(json);
        Console.WriteLine("Metadata serialized to binary successfully.");
        
    }
}