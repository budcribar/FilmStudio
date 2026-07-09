import json

with open('nickandme.json', 'r') as f:
    data = json.load(f)

scene_numbers = [s['scene_number'] for s in data['scenes']]
print("Total scenes:", len(data['scenes']))
print("Scene numbers in JSON:", scene_numbers)
print("Cumulative duration on disk:", data['cumulative_duration_seconds'])
