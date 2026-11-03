import json,datetime,hashlib,shutil
from pathlib import Path
M=Path(__file__).resolve().parent;D=M.parent;R=D.parents[1]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,d):p.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
o=read(M/'resume-observation.json');assert o['turnStatus']=='inProgress' and o['completedCommands']
now=datetime.datetime.now(datetime.timezone.utc).isoformat()
p=M/'resume-handoff.json';d=read(p)
d.update(state='accepted-active-supervisor-turn-smoke-in-progress',updated_utc=now,observation_file=str(M/'resume-observation.json'),active_turn_id=o['turnId'],supervisor_acceptance_observed=True,real_commands_observed=True,smoke_passed=False,feature_development_dispatched_by_organizer=False,organizer_development_authority_relinquished=True,formal_goal={'reported_status':'paused','status_source':'supervisor acknowledgement in active turn','active_resume_operation':'unavailable to supervisor as reported','organizer_direct_goal_read':False,'work_turn_active':True,'goal_storage_modified':False},remaining_blocker={'action':'resume existing Goal from paused to active','target':d['supervisor'],'reason':'Supervisor reports no exposed operation to set active; no direct Goal API available to organizer','outcome':'unavailable-not-executed; not an approval denial','independent_work':'Supervisor is actively performing migration smoke checks in the accepted turn'},new_supervisor_created=False,model_changed=False,workers_remain_pending_supervisor_dispatch=True)
save(p,d)
manifest=read(R/'project-manifest.json')
save(M/'manifest-before-resume.json',manifest)
manifest['updated_utc']=now;manifest['development_resumed']=True
manifest['development_phase']='post-relocation-minimum-environment-smoke-in-progress; feature development gated'
manifest['formal_goal_status']='paused (reported by supervisor; active resume operation unavailable)'
manifest['resume_handoff']=str(p)
manifest['ownership'].update(role='migration complete; organizer handed off and no longer dispatches development',development_state='supervisor active in received turn; smoke before feature work',formal_product_integrator='01a17a22-17e4-70c0-9792-166ddb86ea42')
save(R/'project-manifest.json',manifest)
note='> 最新交接：整理停寫已解除，既有唯一主管已實際接收並開始搬移後環境煙霧驗收；通過前不派功能開發。Goal 仍回報 paused，工具未提供 active 恢復操作。下方整理結果保留完成當時快照。詳見 [恢復收據](docs/project-consolidation/physical-migration/resume-handoff.json)。\n\n'
p=R/'README.md';s=p.read_text(encoding='utf-8');p.write_text(note+s,encoding='utf-8')
p=D/'HANDOFF.md';s=p.read_text(encoding='utf-8');p.write_text(note.replace('docs/project-consolidation/physical-migration/','physical-migration/')+s,encoding='utf-8')
p=D/'index.html';s=p.read_text(encoding='utf-8');banner='<aside style="padding:16px;border:2px solid #367d67;background:#e8f5ef">最新交接：既有主管已實際接收，正在搬移後最小環境煙霧驗收，功能開發尚待此門檻通過。Goal 回報仍 paused，恢復操作不可用。以下是整理完成當時的快照。<a href="physical-migration/resume-handoff.json">恢復收據</a></aside>';s=s.replace('<h1>專案整併完成・P24 驗收待續</h1>',banner+'<h1>專案整併完成・P24 驗收待續</h1>');p.write_text(s,encoding='utf-8')
hashfile=D/'deliverables-sha256.json';shutil.copy2(hashfile,M/'deliverables-before-resume-sha256.json')
files=read(hashfile)
for row in files:
    f=Path(row['path']);row.update(bytes=f.stat().st_size,sha256=hashlib.sha256(f.read_bytes()).hexdigest())
for f in [M/'resume-handoff.json',M/'resume-observation.json',R/'AGENTS.md']:
    files.append({'path':str(f),'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()})
save(hashfile,files)
print(json.dumps({'state':d['state'],'active_turn':o['turnId'],'goal':d['formal_goal']['reported_status'],'smoke_passed':False,'handoff':str(M/'resume-handoff.json')},ensure_ascii=False))
