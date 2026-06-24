import assert from "node:assert/strict";
import test from "node:test";
import { toPublicAssetUrl } from "../src/modules/asset-library/utilities/assetUrl";

const origin = "https://admin.example.com";

test("toPublicAssetUrl keeps already public asset URLs on the current origin", () => {
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

test("toPublicAssetUrl handles unicode paths", () => {
  assert.equal(
    toPublicAssetUrl("/stores/B2B-store/Page Builder/снимок.png", origin),
    "https://admin.example.com/assets/stores/B2B-store/Page%20Builder/%D1%81%D0%BD%D0%B8%D0%BC%D0%BE%D0%BA.png",
  );
});

test("toPublicAssetUrl normalizes absolute /assets URLs to the current origin", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/assets/stores/B2B-store/hero.png?size=large", origin),
    "https://admin.example.com/assets/stores/B2B-store/hero.png?size=large",
  );
});

test("toPublicAssetUrl keeps external non-assets and data URLs unchanged", () => {
  assert.equal(
    toPublicAssetUrl("https://cdn.example.com/media/hero.png", origin),
    "https://cdn.example.com/media/hero.png",
  );
  assert.equal(toPublicAssetUrl("data:image/png;base64,abc", origin), "data:image/png;base64,abc");
});
