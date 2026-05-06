import os
import glob
import xml.etree.ElementTree as ET

# Keys to add
keys_to_add = {
    "Leave History: {0}": {
        "zh-CN": "请假记录：{0}",
        "zh-TW": "請假記錄：{0}",
        "zh-HK": "請假記錄：{0}",
        "default": "Leave History: {0}"
    },
    "Annual": {
        "zh-CN": "年假",
        "default": "Annual"
    },
    "Sick": {
        "zh-CN": "病假",
        "default": "Sick"
    },
    "Approved": {
        "zh-CN": "已批准",
        "default": "Approved"
    },
    "Days label": {
        "zh-CN": "天",
        "default": "Days"
    },
    "Currently on Leave": {
        "zh-CN": "当前休假中",
        "default": "Currently on Leave"
    },
    "Green - Leave period has finished": {
        "zh-CN": "绿色 - 休假已结束",
        "default": "Green - Leave period has finished"
    },
    "Blue - Approved future leave": {
        "zh-CN": "蓝色 - 已批准的未来休假",
        "default": "Blue - Approved future leave"
    },
    "About Team Calendar": {
        "zh-CN": "关于团队日历",
        "default": "About Team Calendar"
    },
    "Clear": {
        "zh-CN": "清除",
        "default": "Clear"
    },
    "Close Search": {
        "zh-CN": "关闭搜索",
        "default": "Close Search"
    }
}

directory = "src/Aiursoft.EmployeeCenter/Resources/Views/Leave/"
for filename in glob.glob(os.path.join(directory, "TeamCalendar.*.resx")):
    tree = ET.parse(filename)
    root = tree.getroot()
    lang = filename.split('.')[-2]
    
    changed = False
    existing_keys = [data.get('name') for data in root.findall('data')]
    
    for key, values in keys_to_add.items():
        if key not in existing_keys:
            val = values.get(lang, values["default"])
            data = ET.SubElement(root, 'data', name=key)
            data.set('xml:space', 'preserve')
            value = ET.SubElement(data, 'value')
            value.text = val
            changed = True
            
    if changed:
        # pretty print
        ET.indent(tree, space="  ", level=0)
        tree.write(filename, encoding="utf-8", xml_declaration=True)
        print(f"Updated {filename}")
