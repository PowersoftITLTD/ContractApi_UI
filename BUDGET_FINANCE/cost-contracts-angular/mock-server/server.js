const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const { buildPayload, buildProject } = require('./serializer');

const app = express();
app.use(cors());               // allow the Angular dev server (localhost:4200)
const db = JSON.parse(fs.readFileSync(path.join(__dirname, 'db.json'), 'utf8'));

// Whole payload (entity + projects[] + data{})
app.get('/api/payload', (_req, res) => res.json(buildPayload(db)));

// Project registry (cards / switcher)
app.get('/api/projects', (_req, res) => res.json({ entity: db.entity, projects: db.projects }));

// One project's data — the login-gated endpoint the app would call in production
app.get('/api/project/:id', (req, res) => {
  const p = buildProject(db, req.params.id);
  return p ? res.json(p) : res.status(404).json({ error: 'unknown project' });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => console.log(`Cockpit mock API on http://localhost:${PORT}`));
