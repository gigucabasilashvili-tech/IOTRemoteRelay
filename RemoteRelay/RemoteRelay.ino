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
  delay(1000);
  pinMode(relayPin, OUTPUT);

  preferences.begin("config", false);
  
  serverIP = preferences.getString("ip", "192.168.137.1");
  serverPort = preferences.getString("port", "8080");
  serverUrl = "http://" + serverIP + ":" + serverPort + "/";
  
  preferences.end();

  Serial.println("Loaded Target URL: " + serverUrl);

  WiFi.begin(ssid, password);
  Serial.print("WiFi connecting");
  
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
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
  if (Serial.available() > 0) {
    String incomingData = Serial.readStringUntil('\n');
    incomingData.trim();

    if (incomingData.startsWith("CFG:")) {
      int firstColon = incomingData.indexOf(':');
      int secondColon = incomingData.indexOf(':', firstColon + 1);

      if (firstColon != -1 && secondColon != -1) {
        serverIP = incomingData.substring(firstColon + 1, secondColon);
        serverPort = incomingData.substring(secondColon + 1);
        serverUrl = "http://" + serverIP + ":" + serverPort + "/";
        
        preferences.begin("config", false);
        preferences.putString("ip", serverIP);
        preferences.putString("port", serverPort);
        preferences.end();
        
        Serial.print("Configuration saved to Flash: ");
        Serial.println(serverUrl);
      }
    }
  }

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