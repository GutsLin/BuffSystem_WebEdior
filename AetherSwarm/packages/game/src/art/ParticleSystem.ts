import { Application, Container, Graphics } from 'pixi.js';

interface Particle {
  x: number; y: number; vx: number; vy: number;
  life: number; maxLife: number; color: number; size: number;
  graphic: Graphics;
}

export class ParticleSystem {
  private app: Application;
  private particles: Particle[] = [];

  constructor(app: Application) {
    this.app = app;
  }

  init(container: Container): void {
    // Particles added to game container via spawnHit/spawnDeath
  }

  spawnHit(x: number, y: number, color: number): void {
    for (let i = 0; i < 8; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 50 + Math.random() * 100;
      const g = new Graphics();
      g.circle(0, 0, 2 + Math.random() * 3).fill({ color, alpha: 0.8 });
      g.x = x;
      g.y = y;
      this.app.stage.addChild(g);

      this.particles.push({
        x, y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        life: 0.3 + Math.random() * 0.3,
        maxLife: 0.3 + Math.random() * 0.3,
        color,
        size: 2 + Math.random() * 3,
        graphic: g,
      });
    }
  }

  spawnDeath(x: number, y: number, type: string): void {
    const color = type === 'paladin' ? 0xfbbf24 : type === 'stoneBeast' ? 0x92400e : 0x6b21a8;
    for (let i = 0; i < 15; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = 30 + Math.random() * 150;
      const g = new Graphics();
      const size = 2 + Math.random() * 5;
      g.circle(0, 0, size).fill({ color, alpha: 0.9 });
      g.x = x;
      g.y = y;
      this.app.stage.addChild(g);

      this.particles.push({
        x, y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        life: 0.5 + Math.random() * 0.5,
        maxLife: 0.5,
        color,
        size,
        graphic: g,
      });
    }
  }

  update(dt: number): void {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.life -= dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.vx *= 0.95;
      p.vy *= 0.95;
      p.graphic.x = p.x;
      p.graphic.y = p.y;

      const ratio = Math.max(0, p.life / p.maxLife);
      p.graphic.alpha = ratio;
      p.graphic.scale.set(ratio);

      if (p.life <= 0) {
        p.graphic.destroy();
        this.particles.splice(i, 1);
      }
    }
  }
}
