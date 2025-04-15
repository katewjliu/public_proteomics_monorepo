from flask import Flask, jsonify, request, g
import sqlite3
from prometheus_client import Histogram, make_wsgi_app, Counter
from werkzeug.middleware.dispatcher import DispatcherMiddleware
import time

app = Flask(__name__)

# Counter for total HTTP requests
REQUEST_COUNT = Counter(
    'http_requests_total',
    'Total HTTP requests',
    ['method', 'endpoint', 'http_status']
)

# Prometheus Histogram for request latency
REQUEST_LATENCY = Histogram(
    'http_request_duration_seconds',
    'Request latency in seconds',
    ['method', 'endpoint']
)

# Counter for request errors
REQUEST_ERRORS = Counter(
    'http_request_errors_total',
    'Total HTTP request errors',
    ['method', 'endpoint', 'http_status']
)

@app.before_request
def start_timer():
    g.start_time = time.time()

@app.after_request
def stop_timer(response):
    # ⏱ Record latency
    request_latency = time.time() - g.start_time
    REQUEST_LATENCY.labels(
        method=request.method,
        endpoint=request.path
    ).observe(request_latency)

    # 🔢 Count request
    REQUEST_COUNT.labels(
        method=request.method,
        endpoint=request.path,
        http_status=str(response.status_code)
    ).inc()
    
    # ❌ Error Count (for 4xx/5xx)
    if response.status_code >= 400:
        REQUEST_ERRORS.labels(
            method=request.method,
            endpoint=request.path,
            http_status=str(response.status_code)
        ).inc()

    return response

# Expose Prometheus metrics at /metrics
app.wsgi_app = DispatcherMiddleware(app.wsgi_app, {
    '/metrics': make_wsgi_app()
})


# Function to connect to the SQLite database and fetch N smallest files
def get_smallest_files(n=10):
    conn = sqlite3.connect('file_metadata_database.db')
    cursor = conn.cursor()
    
    query = '''
    SELECT file_id, file_name, file_size, md5sum, signedUrl
    FROM files
    ORDER BY file_size ASC
    LIMIT ?
    '''
    
    cursor.execute(query, (n,))
    results = cursor.fetchall()

    files = [{'file_id': row[0], 'file_name': row[1], 'file_size': row[2], 'md5sum': row[3], 'signedUrl': row[4]} for row in results]

    conn.close()
    return files

# Function to connect to the SQLite database and fetch N largest files
def get_largest_files(n=10):
    conn = sqlite3.connect('file_metadata_database.db')
    cursor = conn.cursor()
    
    query = '''
    SELECT file_id, file_name, file_size, md5sum, signedUrl
    FROM files
    ORDER BY file_size DESC
    LIMIT ?
    '''
    
    cursor.execute(query, (n,))
    results = cursor.fetchall()

    files = [{'file_id': row[0], 'file_name': row[1], 'file_size': row[2], 'md5sum': row[3], 'signedUrl': row[4]} for row in results]

    conn.close()
    return files

# Function to connect to the SQLite database and fetch files within a file size range
def get_files_in_size_range(min_size, max_size):
    conn = sqlite3.connect('file_metadata_database.db')
    cursor = conn.cursor()
    
    query = '''
    SELECT file_id, file_name, file_size, md5sum, signedUrl
    FROM files
    WHERE file_size BETWEEN ? AND ?
    ORDER BY file_size ASC
    '''
    
    cursor.execute(query, (min_size, max_size))
    results = cursor.fetchall()

    files = [{'file_id': row[0], 'file_name': row[1], 'file_size': row[2], 'md5sum': row[3], 'signedUrl': row[4]} for row in results]

    conn.close()
    return files

# API endpoint to fetch N smallest files
@app.route('/smallest-files', methods=['GET'])
def smallest_files():
    # Get the 'n' parameter from the URL, if provided; otherwise, default to 10
    n = request.args.get('n', default=10, type=int)
    files = get_smallest_files(n)
    return jsonify(files)

# API endpoint to fetch N largest files
@app.route('/largest-files', methods=['GET'])
def largest_files():
    # Get the 'n' parameter from the URL, if provided; otherwise, default to 10
    n = request.args.get('n', default=10, type=int)
    files = get_largest_files(n)
    return jsonify(files)

# API endpoint to fetch files within a size range
@app.route('/files-in-range', methods=['GET'])
def files_in_range():
    # Get the 'min_size' and 'max_size' parameters from the URL, and provide defaults
    min_size = request.args.get('min_size', default=0, type=int)
    max_size = request.args.get('max_size', default=1000000000, type=int)  # Adjust this default to a reasonable max size
    
    files = get_files_in_size_range(min_size, max_size)
    return jsonify(files)

# Run the Flask app
if __name__ == '__main__':
    app.run(debug=True)
