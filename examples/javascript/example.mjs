// Andy Settings API - JavaScript/TypeScript Example
// Usage: node example.mjs

const BASE_URL = process.env.ANDY_SETTINGS_URL || "https://localhost:5300";
const TOKEN = process.env.ANDY_SETTINGS_TOKEN || "your-jwt-token";

// Skip SSL for local development
process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";

const headers = {
  Authorization: `Bearer ${TOKEN}`,
  "Content-Type": "application/json",
};

async function request(method, path, body) {
  const resp = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!resp.ok) throw new Error(`${resp.status} ${resp.statusText}`);
  return resp.json();
}

// 1. List definitions
console.log("=== List Definitions ===");
const definitions = await request("GET", "/api/definitions");
console.log(JSON.stringify(definitions, null, 2));

// 2. Resolve effective value
console.log("\n=== Resolve Effective Value ===");
const resolved = await request("POST", "/api/effective/resolve", {
  key: "andy.containers.defaultProvider",
  context: { applicationCode: "containers", userId: "user-123" },
});
console.log(JSON.stringify(resolved, null, 2));

// 3. Set a value
console.log("\n=== Set Value ===");
await request("POST", "/api/values", {
  definitionKey: "andy.containers.defaultProvider",
  scopeType: "User",
  scopeId: "user-123",
  valueJson: '"docker"',
});
console.log("Value set successfully");

// 4. Explain resolution
console.log("\n=== Explain Resolution ===");
const explanation = await request("POST", "/api/effective/explain", {
  key: "andy.containers.defaultProvider",
  context: { applicationCode: "containers", userId: "user-123" },
});
console.log(JSON.stringify(explanation, null, 2));

// 5. Export settings
console.log("\n=== Export Settings ===");
const exported = await request(
  "GET",
  "/api/export?applicationCode=containers"
);
console.log(JSON.stringify(exported, null, 2));
