"""Andy Settings API - Python Example"""

import json
import os

import requests

BASE_URL = os.getenv("ANDY_SETTINGS_URL", "https://localhost:5300")
TOKEN = os.getenv("ANDY_SETTINGS_TOKEN", "your-jwt-token")

session = requests.Session()
session.headers.update({"Authorization": f"Bearer {TOKEN}"})
session.verify = False  # Skip SSL for local development


def list_definitions():
    """1. List setting definitions."""
    print("=== List Definitions ===")
    resp = session.get(f"{BASE_URL}/api/definitions")
    resp.raise_for_status()
    print(json.dumps(resp.json(), indent=2))


def resolve_effective():
    """2. Resolve effective value for a key."""
    print("\n=== Resolve Effective Value ===")
    resp = session.post(
        f"{BASE_URL}/api/effective/resolve",
        json={
            "key": "andy.containers.defaultProvider",
            "context": {
                "applicationCode": "containers",
                "userId": "user-123",
            },
        },
    )
    resp.raise_for_status()
    print(json.dumps(resp.json(), indent=2))


def set_value():
    """3. Set a scoped value."""
    print("\n=== Set Value ===")
    resp = session.post(
        f"{BASE_URL}/api/values",
        json={
            "definitionKey": "andy.containers.defaultProvider",
            "scopeType": "User",
            "scopeId": "user-123",
            "valueJson": '"docker"',
        },
    )
    print(f"Set value: {resp.status_code}")


def explain_resolution():
    """4. Explain why a value is active."""
    print("\n=== Explain Resolution ===")
    resp = session.post(
        f"{BASE_URL}/api/effective/explain",
        json={
            "key": "andy.containers.defaultProvider",
            "context": {
                "applicationCode": "containers",
                "userId": "user-123",
            },
        },
    )
    resp.raise_for_status()
    print(json.dumps(resp.json(), indent=2))


def export_settings():
    """5. Export settings as JSON."""
    print("\n=== Export Settings ===")
    resp = session.get(f"{BASE_URL}/api/export", params={"applicationCode": "containers"})
    resp.raise_for_status()
    print(json.dumps(resp.json(), indent=2))


if __name__ == "__main__":
    list_definitions()
    resolve_effective()
    set_value()
    explain_resolution()
    export_settings()
