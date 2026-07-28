# BuffWork Web Editor

面向 Unity 的 Buff、英雄三维和战斗效果配置编辑器。

## 在线访问

- Buff 编辑器：http://117.72.150.120:8082
- 域名：http://www.debugcat.cn（需完成京东云域名备案或合规放行后使用）

## 技术栈

- 前端：Vue 3、TypeScript、Vant 4、Pinia、Vue Router
- 服务端：Node.js、NestJS、TypeScript
- 部署：Docker / Docker Compose
- 持久化：服务端 JSON 文件，通过 Docker Volume 保存

## 已实现功能

- Buff / Debuff / Passive / Aura 配置
- 属性修改器与效果动作动态编辑
- Dota 风格力量、敏捷、智力换算公式配置
- 配置保存、复制和删除
- Unity JSON 预览与下载
- 可交互英雄属性演示页面
- 首次启动自动生成演示 Buff

## 本地开发

```bash
pnpm install
pnpm dev
```

- 前端：http://localhost:5173
- API：http://localhost:3000/api
- 健康检查：http://localhost:3000/api/health

## 构建验证

```bash
pnpm typecheck
pnpm build
```

## Docker 部署

```bash
docker compose up -d --build
```

部署完成后访问 `http://服务器IP:3000`。编辑器数据保存在 `buffwork-data` Docker Volume 中，更新镜像不会删除数据。

如果 3000 端口已被其他服务占用，可以通过 `WEB_PORT` 修改宿主机端口：

```bash
WEB_PORT=8082 docker compose up -d --build
```

## 云服务器建议

1. 使用 Nginx、Caddy 或云负载均衡终止 HTTPS。
2. 不要直接公开未认证的编辑器；生产环境建议在反向代理层增加登录、VPN 或零信任访问控制。
3. 定期备份 Docker Volume 中的 `buffs.json` 和 `attribute-formula.json`。
4. 如需要多人编辑、权限、审计或大量数据，再将 JSON 存储替换为 PostgreSQL；现有 Controller 和前端 API 不需要改变。

## Unity 导出格式

下载接口：

```text
GET /api/export/unity
```

导出文件包含：

- `schemaVersion`
- `exportedAt`
- `attributeFormula`
- `buffs`

枚举使用字符串形式，Unity 端可以使用 `JsonUtility`、Newtonsoft.Json 或 System.Text.Json 对应 DTO 读取。
