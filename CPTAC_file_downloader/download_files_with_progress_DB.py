import requests
import hashlib
import os
import sqlite3

from flask import Flask, render_template, redirect, url_for

# PDC API endpoint
url = "https://pdc.cancer.gov/graphql"

# SQLite Database setup
DB_FILE = 'download_progress.db'

def init_db():
    conn = sqlite3.connect(DB_FILE)
    c = conn.cursor()
    # Create a table to store download progress
    c.execute('''
        CREATE TABLE IF NOT EXISTS download_progress (
            unique_id TEXT PRIMARY KEY,
            study_id TEXT,
            pdc_study_id TEXT,
            file_id TEXT,
            file_name TEXT,
            file_size INTEGER,
            md5sum TEXT,
            generated_md5sum TEXT,
            download_url TEXT,
            status TEXT
        )
    ''')
    conn.commit()
    conn.close()

    # Function to add or update download progress in the SQLite database
def update_download_progress(unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, generated_md5sum, download_url, status):
    conn = sqlite3.connect(DB_FILE)
    c = conn.cursor()
    c.execute('''
        INSERT INTO download_progress (unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, generated_md5sum, download_url, status)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(unique_id) DO UPDATE SET status=excluded.status, generated_md5sum=excluded.generated_md5sum
    ''', (unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, generated_md5sum, download_url, status))
    conn.commit()
    conn.close()

    # Function to check if a file has already been downloaded
def is_file_downloaded(unique_id):
    conn = sqlite3.connect(DB_FILE)
    c = conn.cursor()
    c.execute("SELECT status FROM download_progress WHERE unique_id = ?", (unique_id,))
    result = c.fetchone()
    conn.close()
    return result is not None and result[0] == 'completed'

# Fetch studies with version information
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

# Fetch files per study_id
def fetch_files_per_study(study_id):
    query = f"""
    {{
        filesPerStudy(study_id: "{study_id}") {{
            study_id
            pdc_study_id
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
    return response.json().get('data', {}).get('filesPerStudy', [])

# Function to generate MD5 checksum from raw data
def generate_md5_from_data(data):
    md5_hash = hashlib.md5()
    md5_hash.update(data)
    return md5_hash.hexdigest()

# Function to create a unique identifier
def create_unique_identifier(file):
    return f"{file['study_id']}_{file['file_id']}_{file['file_name']}"

# Function to download and process a file
def download_and_process_file(file):
    study_id = file['study_id']
    pdc_study_id = file['pdc_study_id']
    file_id = file['file_id']
    file_name = file['file_name']
    download_url = file['signedUrl']['url']
    file_size = file['file_size']
    md5sum = file['md5sum']
    unique_id = create_unique_identifier(file)

    # Check if the file is already downloaded
    if is_file_downloaded(unique_id):
        print(f"File {file_name} is already downloaded.")
        return

    # Log the file as in progress
    update_download_progress(unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, None, download_url, 'in_progress')

    try:
        # Download the file
        download_response = requests.get(download_url)
        if download_response.status_code == 200:
            file_path = os.path.join("smallest_files_folder", unique_id)
            os.makedirs("smallest_files_folder", exist_ok=True)
            with open(file_path, 'wb') as f:
                f.write(download_response.content)
            
            # Generate md5 checksum
            generated_md5 = generate_md5_from_data(download_response.content)

            # Log the file as completed
            update_download_progress(unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, generated_md5, download_url, 'completed')
        else:
            print(f"Failed to download {file_name}: {download_response.status_code}")
            update_download_progress(unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, None, download_url, 'failed')

    except Exception as e:
        print(f"Error downloading {file_name}: {str(e)}")
        update_download_progress(unique_id, study_id, pdc_study_id, file_id, file_name, file_size, md5sum, None, download_url, 'failed')



