import { Container, Graphics } from 'pixi.js';
import type { Enemy } from './Enemy';

export class Projectile {
  container: Container;
  body: Graphics;
  x: number;
  y: number;
  vx: number;
  vy: number;
  damage: number;
  lifetime: number;
  maxLifetime: number;
  private trail: Graphics;

  constructor(x: number, y: number, angle: number, damage: number, parent: Container) {
    this.x = x;
    this.y = y;
    this.damage = damage;
    this.maxLifetime = 2;
    this.lifetime = 2;

    const speed = 350;
    this.vx = Math.cos(angle) * speed;
    this.vy = Math.sin(angle) * speed;

    this.container = new Container();
    this.body = new Graphics();
    this.body.circle(0, 0, 4).fill({ color: 0x6d5dfc, alpha: 0.9 });
    this.body.circle(0, 0, 2).fill({ color: 0xffffff, alpha: 0.6 });

    this.trail = new Graphics();
    this.trail.moveTo(0, 0).lineTo(-8, 0).stroke({ color: 0x6d5dfc, alpha: 0.3, width: 2 });
    this.container.addChild(this.trail, this.body);

    this.container.x = x;
    this.container.y = y;
    this.container.rotation = angle;
    this.container.zIndex = 10;

    parent.addChild(this.container);
  }

  update(dt: number): void {
    this.container.x += this.vx * dt;
    this.container.y += this.vy * dt;
    this.lifetime -= dt;
  }

  get alive(): boolean { return this.lifetime > 0; }

  hitEnemy(enemy: Enemy): boolean {
    const dx = this.container.x - enemy.x;
    const dy = this.container.y - enemy.y;
    return dx * dx + dy * dy < 400;
  }
}
