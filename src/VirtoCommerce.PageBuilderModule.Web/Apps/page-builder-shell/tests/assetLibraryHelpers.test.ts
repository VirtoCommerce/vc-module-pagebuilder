import assert from "node:assert/strict";
import test from "node:test";
import { getPreviewUrl, toPublicAssetUrl } from "../src/modules/asset-library/utilities/assetUrl";

const origin = "https://admin.example.com";

test("toPublicAssetUrl canonicalizes public asset paths", () => {
  assert.equal(
    toPublicAssetUrl("/assets/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl prefixes store-relative asset paths", () => {
  assert.equal(
    toPublicAssetUrl("/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl keeps custom public asset mount paths", () => {
  assert.equal(
    toPublicAssetUrl("/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png", origin),
    "https://admin.example.com/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png",
  );
});

test("toPublicAssetUrl handles unicode paths", () => {
  assert.equal(
    toPublicAssetUrl("/stores/B2B-store/Page Builder/снимок.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/%D1%81%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA.png",
  );
});

test("toPublicAssetUrl keeps absolute asset URLs unchanged", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/assets/stores/B2B-store/hero.png?size=large", origin),
    "https://cdn.example.com/assets/stores/B2B-store/hero.png?size=large",
  );
});

test("toPublicAssetUrl keeps external non-assets and data URLs unchanged", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/media/hero.png", origin),
    "https://cdn.example.com/media/hero.png",
  );
  assert.equal(toPublicAssetUrl("data:image/png;base64,abc", origin), "data:image/png;base64,abc");
});

test("getPreviewUrl prefers public entry URL over storage relative URL", () => {
  assert.equal(
    getPreviewUrl({
      type: "blob",
      name: "hero.png",
      url: "/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png",
      relativeUrl: "/stores/B2B-store/Page%20Builder/hero.png",
      modifiedDate: "2026-06-18T20:12:47Z",
    }),
    "http://localhost/custom-public/assets/stores/B2B-store/Page%20Builder/hero.png?t=2026-06-18T20%3A12%3A47Z",
  );
});
