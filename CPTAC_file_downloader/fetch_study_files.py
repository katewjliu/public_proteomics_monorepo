import requests
import json
import csv
import hashlib
import os

# PDC API endpoint
url = "https://pdc.cancer.gov/graphql"

# Step 1: Function to fetch studies with version information
def fetch_study_catalog(acceptDUA):
    query = f"""
    {{
        studyCatalog(acceptDUA: {str(acceptDUA).lower()}) {{
            pdc_study_id
            versions {{
                study_id
                
            }}
        }}
    }}
    """
    response = requests.get(url, params={"query": query})
    return response.json()['data']['studyCatalog']

# Step 2: Fetch study catalog with version information
acceptDUA = True  # Set to True or False based on whether you accept DUA
study_catalog = fetch_study_catalog(acceptDUA)

# Step 3: Process the studies with version information
study_id_list = []
for study in study_catalog:
    pdc_study_id = study['pdc_study_id']
    for version in study['versions']:
        study_id_list.append(version['study_id'])
#print(study_id_list)

# Fetch files per study_id
def fetch_files_per_study(study_id):
    query = f"""
    {{
        filesPerStudy(study_id: "{study_id}") {{
            file_id
            file_name
            file_size
            md5sum
            signedUrl {{
                url
            }}
        }}
    }}
    """
    response = requests.get(url, params={"query": query})
    return response.json()['data']['filesPerStudy']

# Loop over each study_id and fetch file information - time consuming step
def get_all_files_from_studies(study_ids):
    all_files = []
    for study_id in study_ids:
        files = fetch_files_per_study(study_id)
        print(study_id, len(files))
        all_files.extend(files)
        
    return all_files

# List all files from studies
all_files = get_all_files_from_studies(study_id_list)

files_sorted = sorted(all_files, key=lambda x: int(x['file_size']))

csv_all_sorted_files = "all_files_sorted.csv"

with open(csv_all_sorted_files, mode='w', newline='') as csv_file:
    fieldnames = ['file_id', 'file_name', 'file_size', 'md5sum', 'signedUrl']
    writer = csv.DictWriter(csv_file, fieldnames = fieldnames)
    writer.writeheader()
    for file_data in files_sorted:
        writer.writerow(file_data)

'''
smallest_files = files_sorted[:100]

# output csv including file size of all files and checksum of 100 file downloaded
csv_file = "100_smallest_files.csv"
# Write data to CSV (Headers first)
with open(csv_file, mode='w', newline='') as file:
    writer = csv.DictWriter(file, fieldnames=["file_id", "file_name", "file_size", 'md5sum', 'generated_md5sum', 'download_url'])
    writer.writeheader()

# Function to generate MD5 checksum from raw data
def generate_md5_from_data(data):
    md5_hash = hashlib.md5()
    md5_hash.update(data)
    return md5_hash.hexdigest()

# Process and print file IDs and URLs
for file in smallest_files:
    file_id = file['file_id']
    file_name = file['file_name']
    download_url = file['signedUrl']['url']
    file_size = file['file_size']
    md5sum = file['md5sum']
    
    
    print(f"File ID: {file_id}, File Name: {file_name}, File Size: {file_size}, Download URL: {download_url}")
    
    # Optional: You can download the file if needed
    download_response = requests.get(download_url)

    # Define your subdirectory and filename
    subdirectory = "100_smallest_folder"
    file_path = os.path.join(subdirectory, file_name)

    # Ensure the subdirectory exists
    os.makedirs(subdirectory, exist_ok=True)

    with open(file_path, 'wb') as f:
        file_content = download_response.content
        f.write(file_content)
    print(f"Downloaded {file_name}")

    # generate md5 checksum from downloaded file content
    generated_md5 = generate_md5_from_data(file_content)

    # export to CSV
    # Append file info to CSV
    with open(csv_file, mode='a', newline='') as file:
        writer = csv.DictWriter(file, fieldnames=["file_id", "file_name", "file_size", 'md5sum', 'generated_md5sum', 'download_url'])
        writer.writerow({
            "file_id": file_id,
            "file_name": file_name,
            "file_size": file_size,
            "md5sum": md5sum,
            'generated_md5sum': generated_md5,
            "download_url": download_url
        })
    
'''
