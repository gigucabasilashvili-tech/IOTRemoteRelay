#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <Preferences.h>

// Default credentials
const char* ssid = "TROLDSKOG"; 
const char* password = "giguca12";

String serverIP;
String serverPort;
String serverUrl;

Preferences preferences;
const int relayPin = 2; 
unsigned long lastRequestTime = 0;
const unsigned long requestInterval = 2000; 

void setup() {
  Serial.begin(115200);
  delay(500); // Give serial a split second to stabilize
  pinMode(relayPin, OUTPUT);

  // 1. Load existing settings from Flash Memory
  preferences.begin("config", false);
  serverIP = preferences.getString("ip", "192.168.1.1");
  serverPort = preferences.getString("port", "88888");
  serverUrl = "http://" + serverIP + ":" + serverPort + "/status";
  preferences.end();

  // 2. Broadcast READY immediately so the C# App knows we just rebooted
  Serial.println("READY");
  Serial.println("Loaded Target URL: " + serverUrl);

  // 3. Initiate Wi-Fi Connection
  WiFi.begin(ssid, password);
  Serial.print("WiFi connecting");
  
  int attempts = 0;
  // This loop runs for up to 10 seconds (20 attempts * 500ms)
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    
    // --- CRITICAL FIX: Read Serial commands WHILE waiting for Wi-Fi ---
    if (Serial.available() > 0) {
      String incomingData = Serial.readStringUntil('\n');
      incomingData.trim();

      if (incomingData.startsWith("CFG:")) {
        int firstColon = incomingData.indexOf(':');
        int secondColon = incomingData.indexOf(':', firstColon + 1);

        if (firstColon != -1 && secondColon != -1) {
          serverIP = incomingData.substring(firstColon + 1, secondColon);
          serverPort = incomingData.substring(secondColon + 1);
          serverUrl = "http://" + serverIP + ":" + serverPort + "/status";
          
          preferences.begin("config", false);
          preferences.putString("ip", serverIP);
          preferences.putString("port", serverPort);
          preferences.end();
          
          // Your WinForms App looks for this exact phrase to stop its timeout
          Serial.print("Configuration saved to Flash: ");
          Serial.println(serverUrl);
        }
      }
    }
    // -----------------------------------------------------------------
    
    delay(500);
    Serial.print(".");
    attempts++;
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\nWiFi connected IP: " + WiFi.localIP().toString());
  } else {
    Serial.println("\nWiFi setup timed out");
  }
}

void loop() {
  // Check for configuration commands during normal runtime operation
  if (Serial.available() > 0) {
    String incomingData = Serial.readStringUntil('\n');
    incomingData.trim();

    if (incomingData.startsWith("CFG:")) {
      int firstColon = incomingData.indexOf(':');
      int secondColon = incomingData.indexOf(':', firstColon + 1);

      if (firstColon != -1 && secondColon != -1) {
        serverIP = incomingData.substring(firstColon + 1, secondColon);
        serverPort = incomingData.substring(secondColon + 1);
        serverUrl = "http://" + serverIP + ":" + serverPort + "/status";
        
        preferences.begin("config", false);
        preferences.putString("ip", serverIP);
        preferences.putString("port", serverPort);
        preferences.end();
        
        Serial.print("Configuration saved to Flash: ");
        Serial.println(serverUrl);
      }
    }
  }

  // Handle background HTTP status updates back to the C# Desktop application
  if (WiFi.status() == WL_CONNECTED) {
    if (millis() - lastRequestTime >= requestInterval) {
      lastRequestTime = millis();
      
      HTTPClient http;
      http.begin(serverUrl);
      
      int httpResponseCode = http.GET();

      if (httpResponseCode > 0) {
        String payload = http.getString();
        Serial.println("Response: " + payload);

        StaticJsonDocument<250> doc;
        DeserializationError error = deserializeJson(doc, payload);

        if (!error) {
          const char* state = doc["state"];
          if (state != nullptr && String(state) == "OFF") {
            digitalWrite(relayPin, LOW);
          } else {
            digitalWrite(relayPin, HIGH);
          }
        }
      } else {
        Serial.print("HTTP Error: ");
        Serial.println(httpResponseCode);
      }
      http.end();
    }
  }
}