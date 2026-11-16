import settings from '../../../../data/settings.json';

describe('bundled settings', () => {
  // The builder API checks the builder's publish permission and the store; the Content module's publishing does not.
  it('publishes file pages through the builder API on either flow', () => {
    const { publish } = settings;

    expect(publish.status).toMatch(/^\/api\/pagebuilder\/git\/publish-status\?/);
    expect(publish.publish.url).toMatch(/^\/api\/pagebuilder\/git\/publish\?/);
    expect(publish.unpublish.url).toMatch(/^\/api\/pagebuilder\/git\/unpublish\?/);
  });
});
