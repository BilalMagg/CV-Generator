import urllib.request
import json
import urllib.error

user_id = '34f9a4d3-b491-4ee4-946a-b04d37788ff8'
payload = {
    'user_id': user_id,
    'job_requirements': {
        'job_role': 'Software Engineer',
        'extracted_skills': ['Python', 'Angular', 'Java'],
        'keywords': ['Docker']
    }
}
data = json.dumps(payload).encode('utf-8')
req = urllib.request.Request('http://localhost:8002/api/v1/match', data=data, headers={'Content-Type': 'application/json'})

try:
    with urllib.request.urlopen(req, timeout=30) as response:
        print('API Response:', response.status)
        print(response.read().decode('utf-8'))
except urllib.error.URLError as e:
    print(f"URL Error: {e}")
except Exception as e:
    print(f"Error: {e}")
