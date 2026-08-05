import type { Hero } from './Hero';
import type { Enemy, EnemyType } from './Enemy';
import type { Projectile } from './Projectile';
import type { GameState } from './Game';
import { Container } from 'pixi.js';
import type { SpriteGenerator } from '../art/SpriteGenerator';

const ENEMY_TYPES: EnemyType[] = ['voidWalker', 'voidWalker', 'stoneBeast', 'elementMage', 'paladin'];

export class SpawnSystem {
  private spriteGen: SpriteGenerator;
  private spawnTimer: number = 0;
  private spawnInterval: number = 2;

  constructor(spriteGen: SpriteGenerator) {
    this.spriteGen = spriteGen;
  }

  update(dt: number, state: GameState, enemies: Enemy[], container: Container, hero: Hero): void {
    this.spawnTimer += dt;
    this.spawnInterval = Math.max(0.3, 2 - state.time / 60);

    if (this.spawnTimer >= this.spawnInterval) {
      this.spawnTimer = 0;
      const count = 1 + Math.floor(state.time / 120);
      for (let i = 0; i < count; i++) {
        this.spawnEnemy(state, enemies, container, hero);
      }
    }
  }

  private spawnEnemy(state: GameState, enemies: Enemy[], container: Container, hero: Hero): void {
    const tier = Math.min(ENEMY_TYPES.length - 1, Math.floor(state.time / 180) + Math.floor(state.wave / 3));
    const type = ENEMY_TYPES[Math.min(tier, ENEMY_TYPES.length - 1)];
    const enemy = new Enemy(type, container);

    const angle = Math.random() * Math.PI * 2;
    const dist = 400 + Math.random() * 200;
    enemy.container.x = hero.x + Math.cos(angle) * dist;
    enemy.container.y = hero.y + Math.sin(angle) * dist;

    enemies.push(enemy);
  }
}
