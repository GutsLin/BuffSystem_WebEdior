<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, nextTick } from 'vue';
import type { HeroType } from './engine/Game';
import { Game, formatTime } from './engine/Game';

const API = '/api';

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const r = await fetch(`${API}${url}`, { headers: { 'Content-Type': 'application/json' }, ...init });
  if (!r.ok) throw new Error(await r.text());
  return r.json() as Promise<T>;
}

function toast(msg: string, type: 'ok' | 'err' = 'ok') {
  const el = document.createElement('div');
  el.className = `game-toast toast-${type}`;
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 2000);
}

const heroTypes: { key: HeroType; label: string; desc: string; icon: string }[] = [
  { key: 'Strength', label: '力量', desc: '高血量 / 高护甲', icon: '💪' },
  { key: 'Agility', label: '敏捷', desc: '高移速 / 快攻速', icon: '🏃' },
  { key: 'Intelligence', label: '智力', desc: '高伤害 / 高魔抗', icon: '🧠' },
  { key: 'Universal', label: '全才', desc: '均衡发展', icon: '⚡' },
];

const phase = ref<'menu' | 'playing' | 'dead'>('menu');
const playerName = ref('');
const selectedHero = ref<HeroType>('Strength');
const finalTime = ref(0);
const finalKills = ref(0);
const submitting = ref(false);
const submitted = ref(false);
const leaderboard = ref<Array<{ name: string; heroType: string; time: number; kills: number; rank: number }>>([]);
const showLeaderboard = ref(false);

let game: Game | null = null;
const canvasRef = ref<HTMLCanvasElement | null>(null);

async function startGame() {
  if (!playerName.value.trim()) { toast('请输入玩家名', 'err'); return; }
  phase.value = 'playing';
  submitted.value = false;
  await nextTick();
  const canvas = canvasRef.value;
  if (!canvas) return;
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
  game = new Game(canvas, selectedHero.value);
  await game.init(canvas);
  window.addEventListener('game-over', onGameOver as EventListener);
}

function onGameOver(e: CustomEvent) {
  finalTime.value = e.detail.time;
  finalKills.value = e.detail.kills;
  phase.value = 'dead';
}

async function submitScore() {
  if (submitted.value || submitting.value || !playerName.value.trim()) return;
  submitting.value = true;
  try {
    await api('/score', { method: 'POST', body: JSON.stringify({ name: playerName.value.trim(), heroType: selectedHero.value, time: finalTime.value, kills: finalKills.value }) });
    submitted.value = true;
    toast('成绩已提交');
  } catch (err) { toast((err as Error).message, 'err'); }
  finally { submitting.value = false; }
}

async function loadLeaderboard() {
  try { leaderboard.value = await api('/leaderboard'); showLeaderboard.value = true; }
  catch (err) { toast((err as Error).message, 'err'); }
}

function restart() { game?.destroy(); game = null; phase.value = 'menu'; }
onBeforeUnmount(() => { game?.destroy(); window.removeEventListener('game-over', onGameOver as EventListener); });
</script>

<template>
  <div class="game-wrapper">
    <div v-if="phase === 'menu'" class="game-menu">
      <div class="menu-header"><h1>⚔ 源能狂潮</h1><p>Aether Swarm · 幸存者割草</p></div>
      <div class="menu-section">
        <label>玩家名</label>
        <input v-model="playerName" class="name-input" placeholder="输入你的名字" maxlength="12" @keyup.enter="startGame" />
      </div>
      <div class="menu-section">
        <label>选择英雄</label>
        <div class="hero-grid">
          <button v-for="h in heroTypes" :key="h.key" :class="['hero-card', { active: selectedHero === h.key }]" @click="selectedHero = h.key">
            <span class="hero-icon">{{ h.icon }}</span><strong>{{ h.label }}</strong><small>{{ h.desc }}</small>
          </button>
        </div>
      </div>
      <div class="menu-actions">
        <button class="start-btn" :disabled="!playerName.value.trim()" @click="startGame">开始游戏</button>
        <button class="lb-btn" @click="loadLeaderboard">🏆 排行榜</button>
      </div>
    </div>

    <canvas v-show="phase === 'playing'" ref="canvasRef" class="game-canvas" />

    <div v-if="phase === 'dead'" class="death-screen">
      <h2>💀 阵亡</h2>
      <p>存活: {{ formatTime(finalTime) }}</p>
      <p>击杀: {{ finalKills }}</p>
      <p>英雄: {{ heroTypes.find(h => h.key === selectedHero)?.label }}</p>
      <div class="death-actions">
        <button v-if="!submitted" class="submit-btn" :disabled="submitting" @click="submitScore">{{ submitting ? '提交中...' : '提交成绩' }}</button>
        <span v-else class="submitted-ok">✓ 已提交</span>
        <button class="restart-btn" @click="restart">再来一局</button>
        <button class="lb-btn" @click="loadLeaderboard">🏆 排行榜</button>
      </div>
    </div>

    <div v-if="showLeaderboard" class="lb-overlay" @click.self="showLeaderboard = false">
      <div class="lb-modal">
        <h2>🏆 排行榜</h2>
        <table class="lb-table">
          <thead><tr><th>#</th><th>玩家</th><th>英雄</th><th>时间</th><th>击杀</th></tr></thead>
          <tbody><tr v-for="row in leaderboard" :key="row.rank"><td>{{ row.rank }}</td><td>{{ row.name }}</td><td>{{ row.heroType }}</td><td>{{ formatTime(row.time) }}</td><td>{{ row.kills }}</td></tr></tbody>
        </table>
        <button class="restart-btn" style="margin-top:12px;width:100%" @click="showLeaderboard = false">关闭</button>
      </div>
    </div>
  </div>
</template>

<style>
@import url('https://unpkg.com/vant@4/lib/index.css');
</style>

<style scoped>
.game-wrapper { width: 100%; height: 100vh; overflow: hidden; background: #0a0a1a; position: relative; }
.game-canvas { width: 100%; height: 100%; display: block; }
.game-menu, .death-screen { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; background: radial-gradient(ellipse at center, #1a1030, #0a0a1a); color: #fff; z-index: 10; padding: 30px; }
.menu-header { text-align: center; margin-bottom: 30px; }
.menu-header h1 { font-size: 36px; margin: 0; background: linear-gradient(135deg, #6d5dfc, #e05461); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
.menu-header p { color: #888; margin: 5px 0 0; }
.menu-section { width: 100%; max-width: 420px; margin-bottom: 20px; }
.menu-section label { display: block; font-size: 13px; color: #888; margin-bottom: 8px; text-transform: uppercase; letter-spacing: 1px; }
.name-input { width: 100%; padding: 12px; border-radius: 10px; border: 1px solid #333; background: #1a1a2e; color: #fff; font-size: 16px; text-align: center; outline: none; }
.hero-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }
.hero-card { padding: 14px; border-radius: 12px; border: 2px solid #2a2a3e; background: #1a1a2e; color: #ccc; cursor: pointer; text-align: center; transition: all 0.2s; }
.hero-card:hover, .hero-card.active { border-color: #6d5dfc; background: rgba(109,93,252,0.15); color: #fff; }
.hero-icon { font-size: 28px; display: block; margin-bottom: 4px; }
.hero-card strong { display: block; font-size: 15px; }
.hero-card small { display: block; font-size: 11px; color: #666; margin-top: 2px; }
.menu-actions, .death-actions { display: flex; flex-direction: column; gap: 10px; margin-top: 20px; width: 100%; max-width: 300px; }
.start-btn, .submit-btn { padding: 14px; border: 0; border-radius: 12px; background: linear-gradient(135deg, #6d5dfc, #e05461); color: #fff; font-size: 18px; font-weight: bold; cursor: pointer; }
.start-btn:disabled { opacity: 0.4; cursor: default; }
.restart-btn, .lb-btn { padding: 12px; border: 1px solid #333; border-radius: 10px; background: #1a1a2e; color: #ccc; font-size: 14px; cursor: pointer; }
.submitted-ok { color: #4ade80; font-size: 14px; text-align: center; }
.death-screen h2 { font-size: 32px; margin: 0 0 15px; color: #e05461; }
.death-screen p { margin: 4px 0; font-size: 16px; color: #ccc; }
.lb-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 100; }
.lb-modal { background: #1a1a2e; border-radius: 16px; padding: 24px; width: 90%; max-width: 500px; max-height: 70vh; overflow-y: auto; color: #fff; }
.lb-modal h2 { margin: 0 0 16px; text-align: center; }
.lb-table { width: 100%; border-collapse: collapse; }
.lb-table th, .lb-table td { padding: 8px 10px; text-align: center; border-bottom: 1px solid #2a2a3e; font-size: 13px; }
.lb-table th { color: #6d5dfc; font-size: 11px; text-transform: uppercase; }
</style>
