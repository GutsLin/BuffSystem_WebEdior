import { Container, Graphics } from 'pixi.js';
import type { Hero } from './Hero';

export type EnemyType = 'voidWalker' | 'stoneBeast' | 'elementMage' | 'paladin';

export class EnemyConfig {
  static get(type: EnemyType): { hp: number; speed: number; damage: number; color: number; size: number; xp: number } {
    switch (type) {
      case 'voidWalker': return { hp: 15, speed: 80, damage: 8, color: 0x6b21a8, size: 12, xp: 5 };
      case 'stoneBeast': return { hp: 80, speed: 40, damage: 20, color: 0x92400e, size: 22, xp: 20 };
      case 'elementMage': return { hp: 25, speed: 50, damage: 25, color: 0x2563eb, size: 14, xp: 15 };
      case 'paladin': return { hp: 150, speed: 35, damage: 30, color: 0xca8a04, size: 26, xp: 50 };
    }
  }
}

export class Enemy {
  container: Container;
  body: Graphics;
  type: EnemyType;
  hp: number;
  maxHp: number;
  speed: number;
  damage: number;
  xp: number;
  private attackCooldown: number = 0;
  private flashTimer: number = 0;

  constructor(type: EnemyType, parent: Container) {
    const cfg = EnemyConfig.get(type);
    this.type = type;
    this.hp = cfg.hp;
    this.maxHp = cfg.hp;
    this.speed = cfg.speed;
    this.damage = cfg.damage;
    this.xp = cfg.xp;

    this.container = new Container();
    this.body = new Graphics();
    this.drawBody(cfg.color, cfg.size);
    this.container.addChild(this.body);

    const hpBar = new Graphics();
    hpBar.rect(-cfg.size, -cfg.size - 6, cfg.size * 2, 3).fill({ color: 0x333333 });
    hpBar.rect(-cfg.size, -cfg.size - 6, cfg.size * 2, 3).fill({ color: 0xe05461 });
    hpBar.name = 'hpBar';
    this.container.addChild(hpBar);

    parent.addChild(this.container);
  }

  private drawBody(color: number, size: number): void {
    this.body.clear();
    if (this.type === 'voidWalker') {
      this.body.regularPoly(0, 0, size, 6).fill({ color, alpha: 0.9 });
      this.body.circle(0, 0, size * 0.4).fill({ color: 0xffffff, alpha: 0.2 });
    } else if (this.type === 'stoneBeast') {
      this.body.rect(-size, -size * 0.8, size * 2, size * 1.6).fill({ color, alpha: 0.9 });
      this.body.rect(-size * 0.6, -size * 0.5, size * 1.2, size).fill({ color: 0xb5651d, alpha: 0.5 });
    } else if (this.type === 'elementMage') {
      this.body.regularPoly(0, 0, size, 4, Math.PI / 4).fill({ color, alpha: 0.9 });
      this.body.circle(0, 0, size * 0.3).fill({ color: 0x60a5fa, alpha: 0.8 });
    } else if (this.type === 'paladin') {
      this.body.moveTo(0, -size);
      this.body.lineTo(size, size * 0.5);
      this.body.lineTo(-size, size * 0.5);
      this.body.closePath().fill({ color, alpha: 0.9 });
      this.body.circle(0, -size * 0.2, size * 0.4).fill({ color: 0xfbbf24, alpha: 0.6 });
    }
  }

  update(dt: number, hero: Hero): void {
    if (this.hp <= 0) return;

    const dx = hero.x - this.container.x;
    const dy = hero.y - this.container.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    if (dist > 1) {
      this.container.x += (dx / dist) * this.speed * dt;
      this.container.y += (dy / dist) * this.speed * dt;
    }

    this.attackCooldown -= dt;
    if (dist < 30 && this.attackCooldown <= 0 && hero.hp > 0) {
      hero.takeDamage(this.damage);
      this.attackCooldown = 1;
    }

    if (this.flashTimer > 0) {
      this.flashTimer -= dt;
      this.body.alpha = this.flashTimer > 0 ? 0.5 : 1;
    }
  }

  takeDamage(amount: number): boolean {
    this.hp -= amount;
    this.flashTimer = 0.1;
    this.body.alpha = 0.5;

    const hpBar = this.container.getChildByName('hpBar') as Graphics;
    if (hpBar) {
      const cfg = EnemyConfig.get(this.type);
      hpBar.clear();
      hpBar.rect(-cfg.size, -cfg.size - 6, cfg.size * 2, 3).fill({ color: 0x333333 });
      hpBar.rect(-cfg.size, -cfg.size - 6, cfg.size * 2 * Math.max(0, this.hp / this.maxHp), 3).fill({ color: 0xe05461 });
    }

    return this.hp <= 0;
  }

  get x(): number { return this.container.x; }
  get y(): number { return this.container.y; }
  get alive(): boolean { return this.hp > 0; }
}
