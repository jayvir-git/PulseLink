import { readFile, writeFile } from 'node:fs/promises';
import { render } from 'pele';

const source = await readFile(new URL('./workflow.mmd', import.meta.url), 'utf8');
const themes = {
  light: { bg: '#f4f7f5', fg: '#13201c', muted: '#4d635b', line: '#0f6a56', surface: '#ffffff', 'surface-alt': '#e3eee8', border: '#73978a', accent: '#0f6a56' },
  dark: { bg: '#10231e', fg: '#e6f2ec', muted: '#b4cabe', line: '#86c9ad', surface: '#1b352c', 'surface-alt': '#254639', border: '#80aa98', accent: '#9adbbd' },
};
for (const [name, colors] of Object.entries(themes)) {
  let { svg } = render(source, { fontFamily: 'Arial, sans-serif', fontSize: 17, padding: 28, idPrefix: 'pulselink-workflow', autoDirection: false, links: false, images: false });
  const variables = Object.entries(colors).map(([key, value]) => `--pele-${key}:${value}`).join(';');
  svg = svg.replace('style="', `style="${variables};background:${colors.bg};`);
  await writeFile(new URL(`../images/workflow-${name}.svg`, import.meta.url), svg + '\n');
}
