import { Container, Graphics } from 'pixi.js';

export class Hero {
  container: Container;
  body: Graphics;
  hp: number;
  maxHp: number;
  targetX?: number;
  targetY?: number;

  constructor(container: Container) {
    this.container = container;
    this.hp = 100;
    this.maxHp = 100;

    this.body = new Graphics();
    this.drawBody();
    container.addChild(this.body);
  }

  private drawBody(): void {
    this.body.clear();
    this.body.circle(0, 0, 18).fill({ color: 0x6d5dfc, alpha: 0.9 });
    this.body.circle(0, 0, 14).fill({ color: 0x8b7dff, alpha: 0.6 });
    this.body.circle(0, 0, 8).fill({ color: 0xffffff, alpha: 0.4 });
  }

  get x(): number { return this.container.x; }
  set x(v: number) { this.container.x = v; this.x = this.container.x; }
  get y(): number { return this.container.y; }
  set y(v: number) { this.container.y = v; this.y = this.container.y; }

  takeDamage(amount: number): void {
    this.hp = Math.max(0, this.hp - amount);
    const ratio = this.hp / this.maxHp;
    const g = Math.floor(255 * ratio);
    const r = Math.floor(255 * (1 - ratio));
    this.body.tint = (r << 16) | (g << 8) | 150;
    setTimeout(() => { this.body.tint = 0xffffff; }, 100);
  }
}
