// Andy Settings API - Go Example
// Usage: go run main.go
package main

import (
	"bytes"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
)

var (
	baseURL = getEnv("ANDY_SETTINGS_URL", "https://localhost:5300")
	token   = getEnv("ANDY_SETTINGS_TOKEN", "your-jwt-token")
)

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func request(method, path string, body any) (map[string]any, error) {
	var bodyReader io.Reader
	if body != nil {
		b, _ := json.Marshal(body)
		bodyReader = bytes.NewReader(b)
	}

	req, err := http.NewRequest(method, baseURL+path, bodyReader)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Authorization", "Bearer "+token)
	req.Header.Set("Content-Type", "application/json")

	// Skip SSL for local development
	client := &http.Client{
		Transport: &http.Transport{
			TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
		},
	}

	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var result map[string]any
	json.NewDecoder(resp.Body).Decode(&result)
	return result, nil
}

func printJSON(label string, data any) {
	b, _ := json.MarshalIndent(data, "", "  ")
	fmt.Printf("=== %s ===\n%s\n\n", label, string(b))
}

func main() {
	// 1. List definitions
	defs, _ := request("GET", "/api/definitions", nil)
	printJSON("List Definitions", defs)

	// 2. Resolve effective value
	ctx := map[string]any{
		"key": "andy.containers.defaultProvider",
		"context": map[string]any{
			"applicationCode": "containers",
			"userId":          "user-123",
		},
	}
	resolved, _ := request("POST", "/api/effective/resolve", ctx)
	printJSON("Resolve Effective Value", resolved)

	// 3. Set a value
	setValue := map[string]any{
		"definitionKey": "andy.containers.defaultProvider",
		"scopeType":     "User",
		"scopeId":       "user-123",
		"valueJson":     `"docker"`,
	}
	request("POST", "/api/values", setValue)
	fmt.Println("=== Value set successfully ===\n")

	// 4. Explain resolution
	explanation, _ := request("POST", "/api/effective/explain", ctx)
	printJSON("Explain Resolution", explanation)

	// 5. Export settings
	exported, _ := request("GET", "/api/export?applicationCode=containers", nil)
	printJSON("Export Settings", exported)
}
