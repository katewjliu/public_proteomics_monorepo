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

### create_DB_sqlite3.py
This Python script reads file metadata stored in a CSV file and imports that data into a SQLite database.
1. **Create SQLite Database** called file_metadata_database.db
2. **Create a table to store CSV data:** defined SQL schema:
   - file_id (primary key)
   - file_name
   - file_size
   - md5sum
   - signedURL
3. **Read and import CSV data:** opens all_files_sorted.csv and read row by row into SQL table (replace existing entries with new data if same primary key)
4. **Save changes and close database connection**

### API_get_files.py
This code provides a web service using Flask framework that interacts with a SQLite database and integrates Prometheus for monitoring HTTP request metrics. 
1. **Prometheus Metrics Setup:**
   - request_count (counter)
   - request_latency (histogram)
   - request_errors (counter)
2. **Request Timing Middleware:** record time before and after request
3. **Prometheus Metrics Endpoint**
4. **SQLite Database Query Functions**
   - get_smallest_files
   - get_largest_files
   - get_files_in_size_range(min_size, max_size)
5. **API endpoints:**
   - /smallest_files, GET method
   - /largest_files, GET method
   - /files-in-range, GET method