import os
import re

dirs = [
    r'd:\Git\Server\RhythmRPG\RhythmRPG\Client\Assets\5.Data\NewSkills',
    r'd:\Git\Server\RhythmRPG\RhythmRPG\Server\GameServer\Content\01.Game\Skill\Json'
]

def migrate_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    def repl_total(m):
        val = int(m.group(3)) * 480
        return f'{m.group(1)}TotalDurationTicks{m.group(1)}{m.group(2)}{val}'
        
    def repl_duration(m):
        val = int(m.group(3)) * 480
        return f'{m.group(1)}DurationTicks{m.group(1)}{m.group(2)}{val}'

    def repl_trigger(m):
        val = int(m.group(3)) * 480
        return f'{m.group(1)}TriggerTick{m.group(1)}{m.group(2)}{val}'

    out1 = re.sub(r'("?)TotalDurationBeats\1(\s*:\s*)(\d+)', repl_total, content)
    out2 = re.sub(r'("?)DurationBeats\1(\s*:\s*)(\d+)', repl_duration, out1)
    out3 = re.sub(r'("?)TriggerBeat\1(\s*:\s*)(\d+)', repl_trigger, out2)

    if content != out3:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(out3)
        print(f"Migrated {filepath}")

for d in dirs:
    if not os.path.exists(d): continue
    for f in os.listdir(d):
        if f.endswith('.asset') or f.endswith('.json'):
            migrate_file(os.path.join(d, f))
