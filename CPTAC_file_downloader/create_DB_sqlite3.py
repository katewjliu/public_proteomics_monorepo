import sqlite3
import csv

# Step 1: Connect to (or create) the SQLite database
conn = sqlite3.connect('file_metadata_database.db')
cursor = conn.cursor()

# Step 2: Create a table for the CSV data
# Primary key ensures that only unique files are added
cursor.execute('''
    CREATE TABLE IF NOT EXISTS files (
        file_id TEXT PRIMARY KEY,
        file_name TEXT NOT NULL,
        file_size INTEGER,
        md5sum TEXT,
        signedUrl TEXT
    )
''')

# Step 3: Read the CSV file and insert data into the SQLite database
csv_file = 'all_files_sorted.csv'  # Replace with your actual CSV file path

with open(csv_file, 'r') as file:
    csv_reader = csv.DictReader(file)
    
    for row in csv_reader:
        cursor.execute('''
            INSERT OR REPLACE INTO files (file_id, file_name, file_size, md5sum, signedUrl)
            VALUES (?, ?, ?, ?, ?)
        ''', (row['file_id'], row['file_name'], row['file_size'], row['md5sum'], row['signedUrl']))

# Step 4: Commit the changes and close the connection
conn.commit()
conn.close()

print("CSV data has been successfully inserted into the SQLite database.")
