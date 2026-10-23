import pathlib, re, json
root=pathlib.Path.cwd()
guid_paths={}
for base in ('Assets','Packages','Library/PackageCache'):
    for p in (root/base).rglob('*.meta'):
        match=re.search(r'^guid: ([0-9a-f]{32})',p.read_text(errors='replace'),re.M)
        if match: guid_paths[match[1]]=str(p)
targets=[root/'Assets/FarmScene.unity']+list((root/'Assets/FarmingSimulation').rglob('*.prefab'))+list((root/'Assets/FarmingSimulation/Animals').rglob('*.controller'))+list((root/'Assets/FarmingSimulation/Animals').rglob('*.mat'))
errors=[]; count=0
for p in targets:
    text=p.read_text(errors='replace')
    ids=set(re.findall(r'^--- !u!\d+ &(-?\d+)',text,re.M))
    for ref in re.findall(r'\{fileID:[^}]+\}',text):
        count+=1
        fid=re.search(r'fileID: (-?\d+)',ref).group(1)
        guid=re.search(r'guid: ([0-9a-f]{32})',ref)
        if guid:
            value=guid.group(1)
            if not value.startswith('0000000000000000') and value not in guid_paths: errors.append(f'{p}: missing GUID {value}')
        elif fid!='0' and fid not in ids: errors.append(f'{p}: missing local ID {fid}')
for p in (root/'Assets/FarmingSimulation').rglob('*'):
    if p.suffix!='.meta' and not pathlib.Path(str(p)+'.meta').exists(): errors.append(f'Missing meta {p}')
result={'files':len(targets),'references':count,'errors':errors}
(root/'.phase7-work/reference-audit.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
raise SystemExit(bool(errors))
