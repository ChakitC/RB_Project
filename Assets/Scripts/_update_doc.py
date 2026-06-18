import os
base = os.path.dirname(os.path.abspath(__file__))
docpath = os.path.normpath(os.path.join(base, '..', '..', 'Docs', 'SYSTEMS', 'SKILL_SYSTEM.md'))
with open(docpath, 'r', encoding='utf-8') as f:
    lines = f.readlines()
out = []
for i, line in enumerate(lines):
    out.append(line)
    if '- SpawnPickupSkillPayloadDef' in line:
        out.append('- TauntSkillPayloadDef' + chr(10))
with open(docpath, 'w', encoding='utf-8') as f:
    f.writelines(out)
print('OK')