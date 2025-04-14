# public_proteomics_monorepo

## CPTAC file downloader project
This Python-based application accesses CPTAC data available on Proteomic Data Commons (PDC) data repository to fetch project and study information as well as download URL via their GraphQL API (https://proteomic.datacommons.cancer.gov/pdc/api-documentation). It creates a SQLite database for this metadata information. Users of this API can query the DB to retrieve file names based on file size (e.g. smallest, largest, or in size range). Users can also start file downloading in parallel threads and monitor file downloading progress on HTML webpage. Overall this project code provides a way to download files from CPTAC programmatically via their API and store file metadata information in a database for future query. 

### System Diagram
![System Diagram](CPTAC_data_download/diagram.svg)

### Individual Script Descriptions

#### fetch_study_files.py
This Python script interacts with a GraphQL API (specifically from the PDC Cancer Data Commons) to retrieve and process study and file data.
1. **Fetching Study Catalog Data:** this fetch function sends a GraphQL query to API endpoint to retrieve studies with version numbers. 
2. **Extracting Study IDs:** from study catalog, iterates over each study and its versions, extracts all study IDs from each version and compiles into a list.
3. **Fetching File Information:** for each study ID in the list, the script sends GraphQL query to retrieve file-related details such as file ID, file name, file size, MD5 checksum and a signed URL.
4. **Sorting and Saving Data:** collected file data is sorted by file size in ascending order, and written to a CSV file (all_files_sorted.csv) for downstream use. 