const state = { project: null, selectedSceneId: null, runtimeSceneId: null, dirty: false };
const $ = (id) => document.getElementById(id);

async function loadProject() {
  const response = await fetch('/api/project');
  if (!response.ok) throw new Error('Could not load game.json');
  state.project = await response.json();
  state.selectedSceneId = state.project.startScene || state.project.scenes[0]?.id;
  state.runtimeSceneId = state.selectedSceneId;
  renderAll();
}

function selectedScene() { return state.project?.scenes.find(scene => scene.id === state.selectedSceneId) || state.project?.scenes[0]; }
function sceneById(id) { return state.project?.scenes.find(scene => scene.id === id); }
function setDirty(value = true) { state.dirty = value; $('save-state').innerHTML = value ? '<span class="status-dot warn"></span> Unsaved changes' : '<span class="status-dot"></span> Saved'; }
function slug(value) { return (value || 'scene').toLowerCase().replace(/[^a-z0-9\u4e00-\u9fff]+/g, '-').replace(/^-|-$/g, '') || 'scene'; }
function uniqueSceneId(base, ignoreId = '') { let id = slug(base), index = 2; while (state.project.scenes.some(scene => scene.id === id && scene.id !== ignoreId)) id = `${slug(base)}-${index++}`; return id; }

function renderAll() { renderProjectMeta(); renderSceneList(); renderEditor(); renderSettings(); renderPreview(); }
function renderProjectMeta() { $('project-title').textContent = state.project.title || 'Untitled'; $('scene-count').textContent = `${state.project.scenes.length} scene${state.project.scenes.length === 1 ? '' : 's'}`; }
function renderSceneList() {
  $('scene-list').innerHTML = state.project.scenes.map((scene, index) => `<button class="scene-item ${scene.id === state.selectedSceneId ? 'selected' : ''}" data-scene-id="${escapeHtml(scene.id)}"><span class="scene-number">${String(index + 1).padStart(2, '0')}</span><span class="scene-item-copy"><strong>${escapeHtml(scene.title || 'Untitled')}</strong><small>${escapeHtml(scene.id)}</small></span><span class="scene-chevron">›</span></button>`).join('');
  document.querySelectorAll('[data-scene-id]').forEach(button => button.addEventListener('click', () => { state.selectedSceneId = button.dataset.sceneId; state.runtimeSceneId = state.selectedSceneId; renderAll(); }));
}
function renderEditor() {
  const scene = selectedScene(); if (!scene) return;
  $('scene-breadcrumb').textContent = scene.id.toUpperCase(); $('scene-heading').textContent = scene.title || 'Untitled scene';
  $('scene-id-input').value = scene.id; $('scene-title-input').value = scene.title; $('background-input').value = scene.background || ''; $('speaker-input').value = scene.speaker || ''; $('character-input').value = scene.character || ''; $('text-input').value = scene.text || ''; $('character-count').textContent = `${(scene.text || '').length} characters`;
  renderStage(scene); renderChoices(scene);
}
function renderStage(scene) { const path = scene.background || ''; $('stage-preview').style.backgroundImage = path ? `url("/api/assets/${encodeURI(path.replace(/^assets\//, ''))}")` : ''; $('stage-preview').classList.toggle('has-image', !!path); $('stage-empty').textContent = path ? path : 'No background selected'; }
function renderChoices(scene) {
  $('choices-list').innerHTML = scene.choices.length ? scene.choices.map((choice, index) => `<div class="choice-row" data-choice-index="${index}"><span class="choice-handle">⠿</span><div class="choice-number">${String(index + 1).padStart(2, '0')}</div><label class="choice-field"><span>Button text</span><input class="choice-text" value="${escapeAttribute(choice.text)}"></label><span class="choice-arrow">→</span><label class="choice-field next-field"><span>Next scene</span><select class="choice-next">${sceneOptions(choice.next)}</select></label><button class="remove-choice" title="Remove choice">×</button></div>`).join('') : '<div class="empty-choices">No choices yet. Add one to let players continue from this scene.</div>';
  document.querySelectorAll('.choice-text').forEach(input => input.addEventListener('input', event => updateChoice(event.target, 'text', event.target.value)));
  document.querySelectorAll('.choice-next').forEach(input => input.addEventListener('change', event => updateChoice(event.target, 'next', event.target.value)));
  document.querySelectorAll('.remove-choice').forEach(button => button.addEventListener('click', event => { const row = event.target.closest('.choice-row'); scene.choices.splice(Number(row.dataset.choiceIndex), 1); setDirty(); renderEditor(); renderPreview(); }));
}
function sceneOptions(selected) { return `<option value="">End / no next scene</option>${state.project.scenes.map(scene => `<option value="${escapeAttribute(scene.id)}" ${scene.id === selected ? 'selected' : ''}>${escapeHtml(scene.title)} · ${escapeHtml(scene.id)}</option>`).join('')}`; }
function updateChoice(target, key, value) { const scene = selectedScene(); const index = Number(target.closest('.choice-row').dataset.choiceIndex); scene.choices[index][key] = value; setDirty(); renderPreview(); }
function renderSettings() { $('game-title-input').value = state.project.title || ''; $('author-input').value = state.project.author || ''; $('version-input').value = state.project.version || ''; $('start-scene-input').innerHTML = state.project.scenes.map(scene => `<option value="${escapeAttribute(scene.id)}" ${scene.id === state.project.startScene ? 'selected' : ''}>${escapeHtml(scene.title)} · ${escapeHtml(scene.id)}</option>`).join(''); }
function renderPreview() {
  const scene = sceneById(state.runtimeSceneId) || selectedScene(); if (!scene) return;
  $('player-scene-label').textContent = (scene.title || 'Untitled').toUpperCase(); $('player-speaker').textContent = scene.speaker || 'Narrator'; $('player-text').textContent = scene.text || ''; $('player-choices').innerHTML = scene.choices.map(choice => `<button class="player-choice" data-runtime-next="${escapeAttribute(choice.next)}">${escapeHtml(choice.text)}</button>`).join('');
  const backdrop = $('player-backdrop'); backdrop.style.backgroundImage = scene.background ? `url("/api/assets/${encodeURI(scene.background.replace(/^assets\//, ''))}")` : ''; backdrop.classList.toggle('with-image', !!scene.background);
  document.querySelectorAll('[data-runtime-next]').forEach(button => button.addEventListener('click', () => { state.runtimeSceneId = button.dataset.runtimeNext || state.runtimeSceneId; renderPreview(); }));
}

async function saveProject() { setDirty(); const response = await fetch('/api/project', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(state.project) }); if (!response.ok) { const payload = await response.json(); showToast(payload.message || 'Save failed', true); return; } state.dirty = false; setDirty(false); showToast('Project saved'); }
async function validateProject() { const response = await fetch('/api/validate'); const payload = await response.json(); renderIssues(payload.issues); if (!payload.issues.length) showToast('No issues found'); }
function renderIssues(issues) { const panel = $('issues-panel'); panel.classList.remove('hidden'); panel.innerHTML = `<div class="issues-header"><strong>${issues.length ? 'Validation results' : 'All clear'}</strong><button class="close-issues">×</button></div>${issues.length ? issues.map(issue => `<div class="issue ${issue.severity}"><span>${issue.severity === 'error' ? '!' : 'i'}</span><div><strong>${escapeHtml(issue.path)}</strong><p>${escapeHtml(issue.message)}</p></div></div>`).join('') : '<p class="clear-message">Your project is ready to build.</p>'}`; $('.close-issues').addEventListener('click', () => panel.classList.add('hidden')); }
function showToast(message, error = false) { const toast = $('toast'); toast.textContent = message; toast.className = `toast visible ${error ? 'error' : ''}`; setTimeout(() => toast.classList.remove('visible'), 2200); }
function escapeHtml(value) { return String(value ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[character])); }
function escapeAttribute(value) { return escapeHtml(value); }

function bindField(id, apply) { $(id).addEventListener('input', event => { apply(event.target.value); setDirty(); renderPreview(); if (id === 'text-input') $('character-count').textContent = `${event.target.value.length} characters`; }); }
bindField('scene-id-input', value => { const scene = selectedScene(); const nextId = uniqueSceneId(value, scene.id); const oldId = scene.id; scene.id = nextId; state.project.scenes.forEach(item => item.choices.forEach(choice => { if (choice.next === oldId) choice.next = nextId; })); state.selectedSceneId = nextId; state.runtimeSceneId = nextId; renderSceneList(); renderEditor(); renderSettings(); });
bindField('scene-title-input', value => { selectedScene().title = value; $('scene-heading').textContent = value || 'Untitled scene'; renderSceneList(); renderSettings(); });
bindField('background-input', value => { selectedScene().background = value; renderStage(selectedScene()); });
bindField('speaker-input', value => { selectedScene().speaker = value; });
bindField('character-input', value => { selectedScene().character = value; });
bindField('text-input', value => { selectedScene().text = value; });
bindField('game-title-input', value => { state.project.title = value; renderProjectMeta(); });
bindField('author-input', value => { state.project.author = value; });
bindField('version-input', value => { state.project.version = value; });
$('start-scene-input').addEventListener('change', event => { state.project.startScene = event.target.value; setDirty(); });
$('save-button').addEventListener('click', saveProject); $('validate-button').addEventListener('click', validateProject); $('restart-preview-button').addEventListener('click', () => { state.runtimeSceneId = state.project.startScene; renderPreview(); });
$('open-player-button').addEventListener('click', () => { window.open('/player-template/index.html', '_blank'); });
$('add-scene-button').addEventListener('click', () => { const scene = { id: uniqueSceneId('new-scene'), title: 'New Scene', background: '', speaker: 'Narrator', text: 'Write your scene here.', choices: [] }; state.project.scenes.push(scene); state.selectedSceneId = scene.id; state.runtimeSceneId = scene.id; setDirty(); renderAll(); });
$('duplicate-scene-button').addEventListener('click', () => { const source = selectedScene(); const scene = JSON.parse(JSON.stringify(source)); scene.id = uniqueSceneId(`${source.id}-copy`); scene.title = `${source.title} copy`; state.project.scenes.push(scene); state.selectedSceneId = scene.id; state.runtimeSceneId = scene.id; setDirty(); renderAll(); });
$('delete-scene-button').addEventListener('click', () => { if (state.project.scenes.length <= 1) return showToast('A project needs at least one scene', true); state.project.scenes = state.project.scenes.filter(scene => scene.id !== state.selectedSceneId); state.selectedSceneId = state.project.startScene = state.project.scenes[0].id; state.runtimeSceneId = state.selectedSceneId; setDirty(); renderAll(); });
$('add-choice-button').addEventListener('click', () => { const scene = selectedScene(); scene.choices.push({ id: `choice-${scene.choices.length + 1}`, text: 'New choice', next: state.project.scenes.find(item => item.id !== scene.id)?.id || '' }); setDirty(); renderEditor(); renderPreview(); });
document.querySelectorAll('[data-tab]').forEach(tab => tab.addEventListener('click', () => { document.querySelectorAll('[data-tab]').forEach(item => item.classList.toggle('active', item === tab)); document.querySelectorAll('.tab-content').forEach(content => content.classList.toggle('active', content.id === `${tab.dataset.tab}-tab`)); }));
window.addEventListener('keydown', event => { if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') { event.preventDefault(); saveProject(); } });
loadProject().catch(error => showToast(error.message, true));

