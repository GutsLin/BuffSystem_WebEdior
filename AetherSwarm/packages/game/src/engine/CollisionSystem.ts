import type { Hero } from './Hero';
import type { Enemy, EnemyType } from './Enemy';
import type { Projectile } from './Projectile';
import type { GameState } from './Game';
import { Container } from 'pixi.js';
import type { ParticleSystem } from '../art/ParticleSystem';
import type { DamageNumbers } from '../art/DamageNumbers';
import type { BuffRuntime } from './BuffRuntime';

export class CollisionSystem {
  private attackTimer: number = 0;

  check(
    hero: Hero,
    enemies: Enemy[],
    projectiles: Projectile[],
    state: GameState,
    particles: ParticleSystem,
    damageNumbers: DamageNumbers,
    buff: BuffRuntime,
  ): void {
    this.attackTimer -= 1 / 60;
    if (this.attackTimer <= 0 && hero.hp > 0) {
      this.autoAttack(hero, enemies, state, particles, damageNumbers, buff);
      this.attackTimer = 0.8;
    }

    // Projectile vs enemy
    for (const proj of projectiles) {
      if (!proj.alive) continue;
      for (const enemy of enemies) {
        if (!enemy.alive) continue;
        if (proj.hitEnemy(enemy)) {
          const killed = enemy.takeDamage(proj.damage);
          damageNumbers.spawn(enemy.x, enemy.y, proj.damage, 'magical');
          particles.spawnHit(enemy.x, enemy.y, 0x6d5dfc);
          proj.lifetime = 0;
          if (killed) {
            this.onKill(enemy, state, particles);
          }
          break;
        }
      }
    }

    // Cleanup
    for (let i = projectiles.length - 1; i >= 0; i--) {
      if (!projectiles[i].alive) {
        projectiles[i].container.destroy();
        projectiles.splice(i, 1);
      }
    }
    for (let i = enemies.length - 1; i >= 0; i--) {
      if (!enemies[i].alive) {
        enemies[i].container.destroy();
        enemies.splice(i, 1);
      }
    }
  }

  private autoAttack(
    hero: Hero,
    enemies: Enemy[],
    state: GameState,
    particles: ParticleSystem,
    damageNumbers: DamageNumbers,
    buff: BuffRuntime,
  ): void {
    let closest: Enemy | null = null;
    let closestDist = 200 * 200;
    for (const enemy of enemies) {
      if (!enemy.alive) continue;
      const dx = hero.x - enemy.x;
      const dy = hero.y - enemy.y;
      const d = dx * dx + dy * dy;
      if (d < closestDist) {
        closestDist = d;
        closest = enemy;
      }
    }
    if (closest) {
      const dmg = 15 + Math.floor(Math.random() * 10);
      const killed = closest.takeDamage(dmg);
      damageNumbers.spawn(closest.x, closest.y, dmg, 'physical');
      particles.spawnHit(closest.x, closest.y, 0xe05461);
      if (killed) this.onKill(closest, state, particles);
    }
  }

  private onKill(enemy: Enemy, state: GameState, particles: ParticleSystem): void {
    state.killCount++;
    state.xp += enemy.xp;
    particles.spawnDeath(enemy.x, enemy.y, enemy.type);

    while (state.xp >= state.xpToNext) {
      state.xp -= state.xpToNext;
      state.level++;
      state.xpToNext = Math.floor(state.xpToNext * 1.3);
    }

    state.wave = Math.floor(state.killCount / 20) + 1;
  }
}
