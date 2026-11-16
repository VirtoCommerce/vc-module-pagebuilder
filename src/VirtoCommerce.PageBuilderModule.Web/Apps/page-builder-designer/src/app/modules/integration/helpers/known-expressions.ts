import { KnownForm, knownForms } from './safe-expression';

// Matched as written, so that themes and settings already deployed keep working.

const templatesWithTheirItem: KnownForm = context =>
  Object.keys(context.response).reduce<Record<string, unknown>>((acc, key) => ({
    ...acc,
    [key]: { ...context.item, ...context.response[key], key, request: undefined },
  }), {});

export const KNOWN_EXPRESSIONS = knownForms([
  [
    'Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], request: undefined, key: x })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})',
    templatesWithTheirItem,
  ],
  [
    'Object.keys(this.response).map(x => ({ ...this.item, ...this.response[x], key: x, request: undefined })).reduce((acc, x) => ({ ...acc, [x.key]: x }), {})',
    templatesWithTheirItem,
  ],
  [
    'JSON.stringify(this.templatesToSave.map(x => ({ path: x.entry.path, groupId: x.entry.groupId, type: x.entry.type, content: x.content })))',
    context => JSON.stringify(context.templatesToSave.map((x: any) => ({
      path: x.entry.path,
      groupId: x.entry.groupId,
      type: x.entry.type,
      content: x.content,
    }))),
  ],
  [
    "(function() { var s = (this.response.settings || []).find(function(x) { return x.name === 'VirtoCommerce.PageBuilderModule.Store.PreviewUserIds' }); return s && s.value ? s.value.split('\\n').map(function(x) { return x.trim() }).filter(Boolean) : [] }).call(this)",
    context => {
      const setting = (context.response.settings || []).find((x: any) => x.name === 'VirtoCommerce.PageBuilderModule.Store.PreviewUserIds');
      return setting && setting.value
        ? String(setting.value).split('\n').map(x => x.trim()).filter(Boolean)
        : [];
    },
  ],
  [
    '(this.response.results || []).map(function(u) { return { id: u.id, name: u.userName || u.email || u.id, photoUrl: u.photoUrl || null } })',
    context => (context.response.results || []).map((u: any) => ({
      id: u.id,
      name: u.userName || u.email || u.id,
      photoUrl: u.photoUrl || null,
    })),
  ],
]);
