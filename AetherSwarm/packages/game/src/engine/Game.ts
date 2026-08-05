import { Application, Container, Text, TextStyle, Graphics, FederatedPointerEvent } from 'pixi.js';
import type { Hero } from './Hero';
import type { Enemy } from './Enemy';
import type { Projectile } from './Projectile';
import { World } from './World';
import { SpawnSystem } from './SpawnSystem';
import { CollisionSystem } from './CollisionSystem';
import { ParticleSystem } from '../art/ParticleSystem';
import { DamageNumbers } from '../art/DamageNumbers';
import { SpriteGenerator } from '../art/SpriteGenerator';
import { BuffRuntime } from './BuffRuntime';

export interface GameState {
  running: boolean;
  paused: boolean;
  score: number;
  killCount: number;
  wave: number;
  time: number;
  level: number;
  xp: number;
  xpToNext: number;
  heroType: 'Strength' | 'Agility' | 'Intelligence' | 'Universal';
}

export type HeroType = 'Strength' | 'Agility' | 'Intelligence' | 'Universal';

export class Game {
  app: Application;
  world: World;
  hero!: Hero;
  enemies: Enemy[] = [];
  projectiles: Projectile[] = [];
  spawnSystem: SpawnSystem;
  collisionSystem: CollisionSystem;
  particleSystem: ParticleSystem;
  damageNumbers: DamageNumbers;
  buffRuntime: BuffRuntime;
  spriteGen: SpriteGenerator;

  state: GameState = {
    running: false,
    paused: false,
    score: 0,
    killCount: 0,
    wave: 1,
    time: 0,
    level: 1,
    xp: 0,
    xpToNext: 20,
    heroType: 'Strength',
  };

  private gameContainer: Container;
  private uiContainer: Container;
  private waveText: Text;
  private xpBar: Graphics;
  private hpBar: Graphics;
  private timerText: Text;
  private killText: Text;
  private keys: Set<string> = new Set();

  constructor(canvas: HTMLCanvasElement, heroType: HeroType) {
    this.app = new Application();
    this.state.heroType = heroType;

    this.spriteGen = new SpriteGenerator();
    this.buffRuntime = new BuffRuntime(heroType);
    this.world = new World(this.app, this.spriteGen);
    this.spawnSystem = new SpawnSystem(this.spriteGen);
    this.collisionSystem = new CollisionSystem();
    this.particleSystem = new ParticleSystem(this.app);
    this.damageNumbers = new DamageNumbers(this.app);

    this.gameContainer = new Container();
    this.uiContainer = new Container();
  }

  async init(canvas: HTMLCanvasElement): Promise<void> {
    await this.app.init({
      canvas,
      width: canvas.clientWidth,
      height: canvas.clientHeight,
      background: '#0a0a1a',
      antialias: true,
      resolution: window.devicePixelRatio || 1,
      autoDensity: true,
    });

    this.app.stage.addChild(this.gameContainer);
    this.app.stage.addChild(this.uiContainer);
    this.world.init(this.gameContainer);
    this.particleSystem.init(this.gameContainer);
    this.damageNumbers.init(this.gameContainer);

    this.hero = this.spriteGen.createHero(this.gameContainer);
    this.hero.x = this.app.screen.width / 2;
    this.hero.y = this.app.screen.height / 2;

    this.setupInput();
    this.setupHUD();
    this.state.running = true;
    this.app.ticker.add(this.update.bind(this));
  }

  private setupInput(): void {
    window.addEventListener('keydown', (e) => this.keys.add(e.key.toLowerCase()));
    window.addEventListener('keyup', (e) => this.keys.delete(e.key.toLowerCase()));

    this.app.stage.eventMode = 'static';
    this.app.stage.hitArea = this.app.screen;
    this.app.stage.on('pointermove', (e: FederatedPointerEvent) => {
      if (e.pointerType === 'touch') {
        this.hero.targetX = e.globalX;
        this.hero.targetY = e.globalY;
      }
    });
  }

  private setupHUD(): void {
    const style = new TextStyle({ fontSize: 14, fill: '#ffffff', fontFamily: 'monospace' });

    this.waveText = new Text({ text: '波次 1', style });
    this.waveText.x = 10;
    this.waveText.y = 10;

    this.timerText = new Text({ text: '00:00', style });
    this.timerText.x = 10;
    this.timerText.y = 30;

    this.killText = new Text({ text: '击杀: 0', style });
    this.killText.x = 10;
    this.killText.y = 50;

    this.xpBar = new Graphics();
    this.xpBar.y = this.app.screen.height - 10;
    this.drawXPBar(0);

    this.hpBar = new Graphics();
    this.hpBar.y = this.app.screen.height - 22;
    this.drawHPBar(1);

    this.uiContainer.addChild(this.waveText, this.timerText, this.killText, this.xpBar, this.hpBar);
  }

  private drawXPBar(ratio: number): void {
    const w = this.app.screen.width;
    this.xpBar.clear();
    this.xpBar.rect(0, 0, w, 8).fill({ color: '#1a1a3a' });
    this.xpBar.rect(0, 0, w * ratio, 8).fill({ color: '#6d5dfc' });
  }

  private drawHPBar(ratio: number): void {
    const w = this.app.screen.width;
    this.hpBar.clear();
    this.hpBar.rect(0, 0, w, 6).fill({ color: '#3a1a1a' });
    this.hpBar.rect(0, 0, w * ratio, 6).fill({ color: '#e05461' });
  }

  update(ticker: { deltaTime: number }): void {
    if (!this.state.running || this.state.paused) return;

    const dt = Math.min(ticker.deltaTime / 60, 0.05);
    this.state.time += dt;

    this.handleInput(dt);
    this.spawnSystem.update(dt, this.state, this.enemies, this.gameContainer, this.hero);
    this.world.update(dt, this.hero);

    for (const enemy of this.enemies) enemy.update(dt, this.hero);
    for (const proj of this.projectiles) proj.update(dt);

    this.collisionSystem.check(this.hero, this.enemies, this.projectiles, this.state, this.particleSystem, this.damageNumbers, this.buffRuntime);

    this.particleSystem.update(dt);
    this.damageNumbers.update(dt);

    if (this.hero.hp <= 0) {
      this.state.running = false;
      this.onDeath();
    }

    this.updateHUD();
    this.world.scrollTo(this.hero.x, this.hero.y);
  }

  private handleInput(dt: number): void {
    const speed = 200 * this.buffRuntime.stats.moveSpeed / 100;
    let dx = 0, dy = 0;
    if (this.keys.has('w') || this.keys.has('arrowup')) dy -= 1;
    if (this.keys.has('s') || this.keys.has('arrowdown')) dy += 1;
    if (this.keys.has('a') || this.keys.has('arrowleft')) dx -= 1;
    if (this.keys.has('d') || this.keys.has('arrowright')) dx += 1;

    if (dx !== 0 || dy !== 0) {
      const len = Math.sqrt(dx * dx + dy * dy);
      dx /= len;
      dy /= len;
      this.hero.x += dx * speed * dt;
      this.hero.y += dy * speed * dt;
    }

    const w = this.app.screen.width;
    const h = this.app.screen.height;
    this.hero.x = Math.max(20, Math.min(w - 20, this.hero.x));
    this.hero.y = Math.max(20, Math.min(h - 60, this.hero.y));
  }

  private updateHUD(): void {
    const mins = Math.floor(this.state.time / 60);
    const secs = Math.floor(this.state.time % 60);
    this.timerText.text = `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
    this.killText.text = `击杀: ${this.state.killCount}`;
    this.waveText.text = `波次 ${this.state.wave} | Lv.${this.state.level}`;
    this.drawXPBar(this.state.xp / this.state.xpToNext);
    this.drawHPBar(Math.max(0, this.hero.hp / this.hero.maxHp));
  }

  private onDeath(): void {
    const text = new Text({
      text: `存活 ${formatTime(this.state.time)} | 击杀 ${this.state.killCount}`,
      style: new TextStyle({ fontSize: 24, fill: '#e05461', fontFamily: 'monospace' }),
    });
    text.anchor.set(0.5);
    text.x = this.app.screen.width / 2;
    text.y = this.app.screen.height / 2;
    this.uiContainer.addChild(text);

    const event = new CustomEvent('game-over', {
      detail: { time: this.state.time, kills: this.state.killCount },
    });
    window.dispatchEvent(event);
  }

  destroy(): void {
    this.state.running = false;
    window.removeEventListener('keydown', () => {});
    this.app.destroy(true);
  }
}

export function formatTime(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}
