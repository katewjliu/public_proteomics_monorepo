import requests
import json
import re
'''
this script calls PRIDE API first to retrieve project details
feed this description into openrouter API prompt
return JSON output
'''

# Define the project identifier
project_id = "PXD001468"

# PRIDE API endpoint for fetching project details
pride_url = f"https://www.ebi.ac.uk/pride/ws/archive/v2/projects/{project_id}"

# Get project details from PRIDE
pride_response = requests.get(pride_url)

if pride_response.status_code == 200:
    try:
        project_details = pride_response.json()
        # Adjust this key based on the actual JSON structure from the PRIDE API.
        #project_description = project_data.get("projectDescription", "No description available")
    except ValueError:
        print("Error: PRIDE API response is not valid JSON.")
        project_details = {}
else:
    print(f"Error: Received status code {pride_response.status_code} from PRIDE API")
    project_details = {}

# Convert the project details JSON object to a formatted string for inclusion in the prompt
project_details_str = json.dumps(project_details, indent=4)
print(project_details_str)
# Construct the prompt for OpenRouter, incorporating the project description from PRIDE.
prompt_content = (
    f"Given this project description, can you extract key experiment information useful for machine learning-based data analysis and return output in JSON format?? "
    f"Below is the project information:\n\n{project_details_str}"
)

# OpenRouter API endpoint and configuration    
response = requests.post(
    url="https://openrouter.ai/api/v1/chat/completions",
    headers={
        "Authorization": "Bearer api_key", # replace with actual API key (private)
        "HTTP-Referer": "<YOUR_SITE_URL>",  # Optional
        "X-Title": "<YOUR_SITE_NAME>",      # Optional
        "Accept": "application/json"
    },
    data=json.dumps({
        "model": "meta-llama/llama-4-maverick",  # Optional
        "response_format": { "type": "json_object" },
        "messages": [
            {
                "role": "user",
                "content": prompt_content

            }
        ]
    })
)


# Parse the JSON response
try:
    result = response.json()
except ValueError:
    print("Error: Response content is not valid JSON.")
    result = None

if result:
    try:
        # Extract the content from the API response (assumes similar structure to OpenAI's API)
        content = result["choices"][0]["message"]["content"]
        #print(content)
        
        # Remove markdown code block markers (``` or ```json)
        content_clean = re.sub(r'^```(?:json)?\n', '', content)
        content_clean = re.sub(r'\n```$', '', content_clean)
        print("Cleaned content:\n", content_clean)
        # Parse the cleaned content into a Python dictionary
        pure_json_output = json.loads(content)

        # Save the pure JSON output to a file
        output_filename = f"output_{project_id}.json"
        with open(output_filename, "w") as outfile:
            json.dump(pure_json_output, outfile, indent=4)
        
        print("Pure JSON output saved to {output_filename}")
    except (KeyError, IndexError, json.JSONDecodeError) as e:
        print("Error processing the assistant's message:", e)

# Check the structure of the returned JSON (assuming it has a similar structure to OpenAI's API)
# For example, it might look like:
# {
#   "id": "chatcmpl-abc123",
#   "object": "chat.completion",
#   "created": 1677858242,
#   "choices": [
#       {
#           "index": 0,
#           "message": {
#              "role": "assistant",
#              "content": "42"
#           },
#           "finish_reason": "stop"
#       }
#   ],
#   "usage": {
#       "prompt_tokens": 9,
#       "completion_tokens": 12,
#       "total_tokens": 21
#   }
# }