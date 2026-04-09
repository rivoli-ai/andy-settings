// Andy Settings API - Rust Example
// Usage: cargo run
// Requires: reqwest, serde_json, tokio in Cargo.toml

use reqwest::header::{AUTHORIZATION, CONTENT_TYPE};
use serde_json::{json, Value};
use std::env;

#[tokio::main]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    let base_url = env::var("ANDY_SETTINGS_URL").unwrap_or_else(|_| "https://localhost:5300".into());
    let token = env::var("ANDY_SETTINGS_TOKEN").unwrap_or_else(|_| "your-jwt-token".into());

    // Skip SSL for local development
    let client = reqwest::Client::builder()
        .danger_accept_invalid_certs(true)
        .build()?;

    // 1. List definitions
    println!("=== List Definitions ===");
    let resp: Value = client
        .get(format!("{base_url}/api/definitions"))
        .header(AUTHORIZATION, format!("Bearer {token}"))
        .send()
        .await?
        .json()
        .await?;
    println!("{}", serde_json::to_string_pretty(&resp)?);

    // 2. Resolve effective value
    println!("\n=== Resolve Effective Value ===");
    let body = json!({
        "key": "andy.containers.defaultProvider",
        "context": {
            "applicationCode": "containers",
            "userId": "user-123"
        }
    });
    let resp: Value = client
        .post(format!("{base_url}/api/effective/resolve"))
        .header(AUTHORIZATION, format!("Bearer {token}"))
        .header(CONTENT_TYPE, "application/json")
        .json(&body)
        .send()
        .await?
        .json()
        .await?;
    println!("{}", serde_json::to_string_pretty(&resp)?);

    // 3. Set a value
    println!("\n=== Set Value ===");
    let set_body = json!({
        "definitionKey": "andy.containers.defaultProvider",
        "scopeType": "User",
        "scopeId": "user-123",
        "valueJson": "\"docker\""
    });
    let status = client
        .post(format!("{base_url}/api/values"))
        .header(AUTHORIZATION, format!("Bearer {token}"))
        .header(CONTENT_TYPE, "application/json")
        .json(&set_body)
        .send()
        .await?
        .status();
    println!("Set value: {status}");

    // 4. Explain resolution
    println!("\n=== Explain Resolution ===");
    let resp: Value = client
        .post(format!("{base_url}/api/effective/explain"))
        .header(AUTHORIZATION, format!("Bearer {token}"))
        .header(CONTENT_TYPE, "application/json")
        .json(&body)
        .send()
        .await?
        .json()
        .await?;
    println!("{}", serde_json::to_string_pretty(&resp)?);

    // 5. Export settings
    println!("\n=== Export Settings ===");
    let resp: Value = client
        .get(format!("{base_url}/api/export?applicationCode=containers"))
        .header(AUTHORIZATION, format!("Bearer {token}"))
        .send()
        .await?
        .json()
        .await?;
    println!("{}", serde_json::to_string_pretty(&resp)?);

    Ok(())
}
