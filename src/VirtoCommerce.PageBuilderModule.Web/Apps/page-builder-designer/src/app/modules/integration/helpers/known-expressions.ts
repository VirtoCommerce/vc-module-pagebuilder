import { registerKnownForm } from './safe-expression';

// Shipped expressions the expression language cannot run: matched as written, whitespace aside, so that
// themes and settings already deployed keep working.

const ABSOLUTE_OR_PROTOCOL_RELATIVE_URL = /^(?:[a-z][a-z\d+\-.]*:)?\/\//i;

// vc-frontend templates/page.json and templates/blogs.json
for (const expression of [
  'Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], request: undefined, key: x })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})',
  'Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], key: x, request: undefined })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})',
]) {
  registerKnownForm(expression, context =>
    Object.keys(context.response).reduce<Record<string, unknown>>((acc, key) => ({
      ...acc,
      [key]: { ...context.item, ...context.response[key], key, request: undefined },
    }), {}));
}

// data/settings.json
registerKnownForm(
  'JSON.stringify(this.templatesToSave.map(x => ({ path: x.entry.path, groupId: x.entry.groupId, type: x.entry.type, content: x.content })))',
  context => JSON.stringify(context.templatesToSave.map((x: any) => ({
    path: x.entry.path,
    groupId: x.entry.groupId,
    type: x.entry.type,
    content: x.content,
  }))),
);

registerKnownForm(
  "(function(url) { if (!url) return ''; var trimmed = url.trim(); if (!trimmed) return ''; if (/^(?:[a-z][a-z\\d+\\-.]*:)?\\/\\//i.test(trimmed) || trimmed.indexOf('data:') === 0) return trimmed; var normalized = trimmed.charAt(0) === '/' ? trimmed : '/' + trimmed; return normalized.toLowerCase().indexOf('/assets/') === 0 ? normalized : combine('/assets', normalized); })(this.assetUrl)",
  (context, functions) => {
    const trimmed = typeof context.assetUrl === 'string' ? context.assetUrl.trim() : '';
    if (!trimmed) {
      return '';
    }
    if (ABSOLUTE_OR_PROTOCOL_RELATIVE_URL.test(trimmed) || trimmed.startsWith('data:')) {
      return trimmed;
    }
    const normalized = trimmed.startsWith('/') ? trimmed : '/' + trimmed;
    return normalized.toLowerCase().startsWith('/assets/') ? normalized : functions['combine']('/assets', normalized);
  },
);

registerKnownForm(
  "(function(publicUrl, modifiedDate) { if (!publicUrl || !modifiedDate) return publicUrl; return publicUrl + (publicUrl.indexOf('?') >= 0 ? '&' : '?') + 't=' + encodeURIComponent(modifiedDate); })(this.publicAssetUrl, this.modifiedDate)",
  context => {
    const { publicAssetUrl, modifiedDate } = context;
    if (!publicAssetUrl || !modifiedDate) {
      return publicAssetUrl;
    }
    return publicAssetUrl + (publicAssetUrl.indexOf('?') >= 0 ? '&' : '?') + 't=' + encodeURIComponent(modifiedDate);
  },
);

registerKnownForm(
  "(function() { var s = (this.response.settings || []).find(function(x) { return x.name === 'VirtoCommerce.PageBuilderModule.Store.PreviewUserIds' }); return s && s.value ? s.value.split('\\n').map(function(x) { return x.trim() }).filter(Boolean) : [] }).call(this)",
  context => {
    const setting = (context.response.settings || []).find((x: any) => x.name === 'VirtoCommerce.PageBuilderModule.Store.PreviewUserIds');
    return setting && setting.value
      ? String(setting.value).split('\n').map(x => x.trim()).filter(Boolean)
      : [];
  },
);

registerKnownForm(
  '(this.response.results || []).map(function(u) { return { id: u.id, name: u.userName || u.email || u.id, photoUrl: u.photoUrl || null } })',
  context => (context.response.results || []).map((u: any) => ({
    id: u.id,
    name: u.userName || u.email || u.id,
    photoUrl: u.photoUrl || null,
  })),
);
