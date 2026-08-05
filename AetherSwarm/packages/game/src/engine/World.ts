import { Application, Container, Graphics, Sprite, Texture } from 'pixi.js';
import type { Hero } from './Hero';
import type { SpriteGenerator } from '../art/SpriteGenerator';

export class World {
  private app: Application;
  private container: Container;
  private mapContainer: Container;
  private spriteGen: SpriteGenerator;
  private scrollX: number = 0;
  private scrollY: number = 0;

  constructor(app: Application, spriteGen: SpriteGenerator) {
    this.app = app;
    this.spriteGen = spriteGen;
    this.container = new Container();
    this.mapContainer = new Container();
  }

  init(parent: Container): void {
    this.container.addChild(this.mapContainer);
    parent.addChild(this.container);
    this.drawMap();
  }

  private drawMap(): void {
    const mapWidth = 3000;
    const mapHeight = 3000;
    const tileSize = 64;

    for (let x = 0; x < mapWidth; x += tileSize) {
      for (let y = 0; y < mapHeight; y += tileSize) {
        const g = new Graphics();
        const shade = (Math.floor(x / tileSize) + Math.floor(y / tileSize)) % 2 === 0 ? 0x0d0d20 : 0x0a0a1a;
        g.rect(x, y, tileSize, tileSize).fill({ color: shade });
        if (x === 0 || y === 0 || x + tileSize >= mapWidth || y + tileSize >= mapHeight) {
          g.rect(x, y, tileSize, tileSize).stroke({ color: 0x1a1a3a, width: 1, alpha: 0.5 });
        }
        this.mapContainer.addChild(g);
      }
    }
  }

  update(dt: number, hero: Hero): void {
    // Map scrolling handled by scrollTo
  }

  scrollTo(x: number, y: number): void {
    const w = this.app.screen.width;
    const h = this.app.screen.height;
    this.scrollX = Math.max(0, Math.min(3000 - w, x - w / 2));
    this.scrollY = Math.max(0, Math.min(3000 - h, y - h / 2));
    this.mapContainer.x = -this.scrollX;
    this.mapContainer.y = -this.scrollY;
  }
}
