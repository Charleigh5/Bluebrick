import { createElement } from "react";
import packageJson from "../package.json";

const UNKNOWN = "UNKNOWN";

type BlueBrickViteEnv = {
  VITE_BLUEBRICK_ENVIRONMENT?: string;
  VITE_BLUEBRICK_SOURCE_COMMIT?: string;
  VITE_BLUEBRICK_BUILD_ID?: string;
  VITE_BLUEBRICK_BUILD_UTC?: string;
};

const viteEnv = import.meta.env as ImportMetaEnv & BlueBrickViteEnv;

function readEnv(name: keyof BlueBrickViteEnv, fallback: string): string {
  const value = viteEnv[name];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : fallback;
}

export type BlueBrickRuntimeIdentity = {
  environment: string;
  product: "BlueBrick 2.0";
  sourceCommit: string;
  buildId: string;
  buildUtc: string;
  packageVersion: string;
  entrypoint: string;
  schemaVersion: "1";
  assetManifest: "index.html,assistant-index.css,assistant-web.js";
  label: string;
};

const environment = readEnv("VITE_BLUEBRICK_ENVIRONMENT", UNKNOWN);
const sourceCommit = readEnv("VITE_BLUEBRICK_SOURCE_COMMIT", UNKNOWN);
const buildId = readEnv("VITE_BLUEBRICK_BUILD_ID", UNKNOWN);

export const runtimeIdentity: BlueBrickRuntimeIdentity = {
  environment,
  product: "BlueBrick 2.0",
  sourceCommit,
  buildId,
  buildUtc: readEnv("VITE_BLUEBRICK_BUILD_UTC", UNKNOWN),
  packageVersion: typeof packageJson.version === "string" ? packageJson.version : UNKNOWN,
  entrypoint: "AssistantWeb/src/main.tsx -> App.tsx",
  schemaVersion: "1",
  assetManifest: "index.html,assistant-index.css,assistant-web.js",
  label: `${environment} | BlueBrick 2.0 | ${sourceCommit} | ${buildId}`
};

export function RuntimeIdentitySurface() {
  return createElement(
    "div",
    {
      className: "bluebrick-runtime-identity",
      "data-bluebrick-runtime-identity": runtimeIdentity.label,
      "data-bluebrick-environment": runtimeIdentity.environment,
      "data-bluebrick-product": runtimeIdentity.product,
      "data-bluebrick-source-commit": runtimeIdentity.sourceCommit,
      "data-bluebrick-build-id": runtimeIdentity.buildId,
      "data-bluebrick-build-utc": runtimeIdentity.buildUtc,
      "data-bluebrick-package-version": runtimeIdentity.packageVersion,
      "data-bluebrick-entrypoint": runtimeIdentity.entrypoint,
      "data-bluebrick-schema-version": runtimeIdentity.schemaVersion,
      "data-bluebrick-asset-manifest": runtimeIdentity.assetManifest,
      "aria-label": `BlueBrick runtime identity: ${runtimeIdentity.label}`
    },
    runtimeIdentity.label
  );
}
