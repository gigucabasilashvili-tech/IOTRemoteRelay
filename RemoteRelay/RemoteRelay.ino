#include <ArduinoJson.h>
#include <Preferences.h>
#include <Adafruit_NeoPixel.h>

// PINS
#define RELAY_PIN 21
#define LED_PIN 4
#define LED_COUNT 8
#define SIM800_RX 13 // ESP32 RX2 <- SIM800 TX
#define SIM800_TX 14 // ESP32 TX2 -> SIM800 RX

// --- SIM800L / GPRS APN
#define SIM800_BAUD 9600
#define APN "internet" 

// --- Timing ---
const unsigned long REQUEST_INTERVAL_MS = 15000; 
const unsigned long SIM800_CMD_TIMEOUT_MS = 15000;

Preferences preferences;
Adafruit_NeoPixel strip(LED_COUNT, LED_PIN, NEO_GRB + NEO_KHZ800);

HardwareSerial sim800(2);

String serverIP = "192.168.1.1"; // default ip
String serverPort = "65050"; // default port
String serverUrl;

unsigned long lastRequestTime = 0;
bool serverConnected = false;
bool relayOn = true; // default relay setting is ON
int httpFailStreak = 0;

// --- WS2812 LED Helpers ---
void setLedColor(uint8_t index, uint8_t r, uint8_t g, uint8_t b) {
  if (index >= LED_COUNT) return;
  strip.setPixelColor(index, strip.Color(r, g, b));
  strip.show();
}

void updateConnectionLed() {
  setLedColor(0, serverConnected ? 0 : 0, serverConnected ? 255 : 0, serverConnected ? 0 : 255);
}

void updateRelayLed() {
  setLedColor(1, relayOn ? 0 : 255, relayOn ? 255 : 0, 0);
}

void initLedStrip() {
  strip.begin();
  strip.setBrightness(40);
  strip.clear();
  strip.show();
  updateConnectionLed();
  updateRelayLed();
}

// --- Config Parser (USB CFG Bridge) ---
void applyConfig(const String& ip, const String& port) {
  serverIP = ip;
  serverPort = port;
  serverUrl = "http://" + serverIP + ":" + serverPort + "/status";

  preferences.begin("config", false);
  preferences.putString("ip", serverIP);
  preferences.putString("port", serverPort);
  preferences.end();

  Serial.print("Configuration saved to Flash: ");
  Serial.println(serverUrl);
}

void pollSerialConfig() {
  while (Serial.available() > 0) {
    String incoming = Serial.readStringUntil('\n');
    incoming.trim();
    if (incoming.startsWith("CFG:")) {
      int firstColon = incoming.indexOf(':');
      int secondColon = incoming.indexOf(':', firstColon + 1);
      if (firstColon != -1 && secondColon != -1) {
        applyConfig(incoming.substring(firstColon + 1, secondColon), incoming.substring(secondColon + 1));
      }
    }
  }
}

void drainSim800() {
  while (sim800.available()) {
    sim800.read();
  }
}

String readSim800Until(unsigned long timeoutMs, const char* stopToken) {
  String response;
  unsigned long start = millis();
  while (millis() - start < timeoutMs) {
    pollSerialConfig();
    while (sim800.available()) {
      char c = sim800.read();
      response += c;
      Serial.write(c);
      if (stopToken != nullptr && response.indexOf(stopToken) != -1) {
        return response;
      }
    }
  }
  return response;
}

bool sendAT(const char* cmd, unsigned long timeoutMs, const char* expect = "OK") {
  drainSim800();
  sim800.print(cmd);
  sim800.print("\r\n");
  String response = readSim800Until(timeoutMs, expect);
  return response.indexOf(expect) != -1;
}

bool ensureGprsOpen() {
  drainSim800();
  sim800.print("AT+SAPBR=2,1\r\n");
  String status = readSim800Until(5000, "OK");
  if (status.indexOf("+SAPBR: 1,1") != -1 && status.indexOf("0.0.0.0") == -1) {
    return true;
  }
  Serial.println("GPRS bearer closed — reopening...");
  if (!sendAT("AT+SAPBR=1,1", 30000, "OK")) return false;
  sendAT("AT+SAPBR=2,1", 5000);
  return true;
}

bool httpGet(const String& url, String& bodyOut) {
  bodyOut = "";

  if (!ensureGprsOpen()) {
    Serial.println("HTTP Failure: No cellular connection.");
    return false;
  }

  sendAT("AT+HTTPTERM", 2000); 
  sendAT("AT+HTTPINIT", 5000);
  sendAT("AT+HTTPPARA=\"CID\",1", 5000);
  
  String urlCmd = "AT+HTTPPARA=\"URL\",\"" + url + "\"";
  sendAT(urlCmd.c_str(), 5000);
  sendAT("AT+HTTPPARA=\"USERDATA\",\"Connection: keep-alive\"", 5000);

  Serial.println("Executing connection transaction...");
  sim800.print("AT+HTTPACTION=0\r\n");

  delay(4000);

  Serial.println("\n>>> Forcing raw content stream download now...");
  drainSim800();
  sim800.print("AT+HTTPREAD\r\n");
  
  String rawDump = "";
  unsigned long streamTimer = millis();
  
  while (millis() - streamTimer < 2500) {
    while (sim800.available()) {
      char c = sim800.read();
      rawDump += c;
      Serial.write(c); 
      streamTimer = millis(); 
    }
  }

  int jsonStart = rawDump.indexOf('{');
  int jsonEnd = rawDump.lastIndexOf('}');
  
  if (jsonStart != -1 && jsonEnd != -1 && jsonEnd > jsonStart) {
    bodyOut = rawDump.substring(jsonStart, jsonEnd + 1);
    bodyOut.trim();
    sendAT("AT+HTTPTERM", 2000);
    return true; 
  }

  Serial.println("\n[Parse Error]: '{' or '}' brackets missing inside the collected raw frame.");
  sendAT("AT+HTTPTERM", 2000);
  return false;
}

void applyRelayStateFromJson(const String& jsonBody) {
  StaticJsonDocument<250> doc;
  DeserializationError error = deserializeJson(doc, jsonBody);
  if (error) {
    Serial.print("JSON parsing fault: ");
    Serial.println(error.c_str());
    return;
  }

  const char* state = doc["state"];
  if (state == nullptr) return;

  relayOn = (String(state) != "OFF");
  
  digitalWrite(RELAY_PIN, relayOn ? LOW : HIGH);
  updateRelayLed();

  Serial.print(">>> SUCCESS: Relay parsed and set to -> ");
  if (relayOn) {
    Serial.println("ON (3.3V / HIGH)");
  } else {
    Serial.println("OFF (0V / LOW)");
  }
}

void pollServerStatus() {
  String body;
  Serial.println("\n--------------------------------------------");
  Serial.print("Polling target destination: ");
  Serial.println(serverUrl);

  if (httpGet(serverUrl, body)) {
    httpFailStreak = 0;
    serverConnected = true;
    updateConnectionLed();
    Serial.print("Valid Data Body Found: ");
    Serial.println(body);
    applyRelayStateFromJson(body);
  } else {
    httpFailStreak++;
    serverConnected = false;
    updateConnectionLed();
    Serial.println("Polling cycle dropped or blank.");

    if (httpFailStreak >= 3) {
      Serial.println("Re-booting GPRS connection profiles due to connection streak fails...");
      sendAT("AT+SAPBR=0,1", 5000);
      delay(500);
      sendAT("AT+SAPBR=1,1", 30000, "OK");
      httpFailStreak = 0;
    }
  }
}

void setup() {
  Serial.begin(115200);
  delay(500);

  // Default state is ON
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, HIGH); 
  relayOn = false;

  initLedStrip();

  preferences.begin("config", false);
  serverIP = preferences.getString("ip", "192.168.1.1");
  serverPort = preferences.getString("port", "65050");
  preferences.end();
  serverUrl = "http://" + serverIP + ":" + serverPort + "/status";

  Serial.println("READY");
  Serial.println("Target endpoint active: " + serverUrl);

  sim800.begin(SIM800_BAUD, SERIAL_8N1, SIM800_RX, SIM800_TX);
  delay(1000);

  sendAT("AT", 1000);
  sendAT("ATE0", 2000);
  sendAT("AT+CGATT=1", 10000);
  sendAT("AT+SAPBR=3,1,\"Contype\",\"GPRS\"", 5000);
  
  String apnCmd = String("AT+SAPBR=3,1,\"APN\",\"") + APN + "\"";
  sendAT(apnCmd.c_str(), 5000);
  sendAT("AT+SAPBR=1,1", 30000);

  pollServerStatus();
}

void loop() {
  pollSerialConfig();

  if (millis() - lastRequestTime >= REQUEST_INTERVAL_MS) {
    pollServerStatus();
    lastRequestTime = millis(); 
  }
}