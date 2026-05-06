import re
import xml.etree.ElementTree as ET

# Read the view file
with open('src/Aiursoft.EmployeeCenter/Views/Leave/TeamCalendar.cshtml', 'r', encoding='utf-8') as f:
    content = f.read()

# Find all @Localizer["key"]
matches = re.findall(r'@Localizer\["(.*?)"(?:,\s*.*?)?\]', content)

# Also find Localizer["key"] inside JS or C# code block (no @)
matches += re.findall(r'Localizer\["(.*?)"(?:,\s*.*?)?\]', content)

matches = list(set(matches))

# Read the resx file
tree = ET.parse('src/Aiursoft.EmployeeCenter/Resources/Views/Leave/TeamCalendar.zh-CN.resx')
root = tree.getroot()
existing_keys = [data.get('name') for data in root.findall('data')]

missing_keys = []
for key in matches:
    if key not in existing_keys:
        missing_keys.append(key)

print(f"Missing keys: {missing_keys}")
