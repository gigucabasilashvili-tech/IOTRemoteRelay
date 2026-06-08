#include <ArduinoJson.h>
#include <Preferences.h>
#include <Adafruit_NeoPixel.h>

// --- Hardware pins ---
#define RELAY_PIN 2
#define LED_PIN 4
#define LED_COUNT 8
#define SIM800_RX 16 // ESP32 RX2 <- SIM800 TX
#define SIM800_TX 17 // ESP32 TX2 -> SIM800 RX

// --- SIM800L / GPRS ---
#define SIM800_BAUD 9600
#define APN "internet" // Change to your carrier APN if needed

// --- Timing ---
const unsigned long REQUEST_INTERVAL_MS = 5000;
const unsigned long SIM800_CMD_TIMEOUT_MS = 15000;

Preferences preferences;
Adafruit_NeoPixel strip(LED_COUNT, LED_PIN, NEO_GRB + NEO_KHZ800);

HardwareSerial sim800(2);

String serverIP = "192.168.1.1";
String serverPort = "65050";
String serverUrl;

unsigned long lastRequestTime = 0;
bool serverConnected = false;
bool relayOn = true;

// --- WS2812 helpers (LED 0 = connection, LED 1 = relay) ---
void setLedColor(uint8_t index, uint8_t r, uint8_t g, uint8_t b) {
  if (index >= LED_COUNT) return;
  strip.setPixelColor(index, strip.Color(r, g, b));
  strip.show();
}

void updateConnectionLed() {
  if (serverConnected) {
    setLedColor(0, 0, 255, 0); // green
  } else {
    setLedColor(0, 0, 0, 255); // blue
  }
}

void updateRelayLed() {
  if (relayOn) {
    setLedColor(1, 0, 255, 0); // green = relay ON
  } else {
    setLedColor(1, 255, 0, 0); // red = relay OFF
  }
}

void initLedStrip() {
  strip.begin();
  strip.setBrightness(40);
  strip.clear();
  strip.show();
  updateConnectionLed();
  updateRelayLed();
}

// --- Serial configuration (CFG:ip:port from WinForms over USB) ---
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

bool tryParseConfigLine(const String& line) {
  if (!line.startsWith("CFG:")) return false;

  int firstColon = line.indexOf(':');
  int secondColon = line.indexOf(':', firstColon + 1);
  if (firstColon == -1 || secondColon == -1) return false;

  applyConfig(line.substring(firstColon + 1, secondColon), line.substring(secondColon + 1));
  return true;
}

void pollSerialConfig() {
  while (Serial.available() > 0) {
    String incoming = Serial.readStringUntil('\n');
    incoming.trim();
    tryParseConfigLine(incoming);
  }
}

// --- SIM800L AT helpers ---
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

bool waitForToken(const char* token, unsigned long timeoutMs) {
  String response = readSim800Until(timeoutMs, token);
  return response.indexOf(token) != -1;
}

bool initSim800() {
  Serial.println("Initializing SIM800L...");

  for (int i = 0; i < 5; i++) {
    if (sendAT("AT", 1000)) break;
    delay(500);
  }

  sendAT("ATE0", 2000);
  sendAT("AT+CPIN?", 5000);
  sendAT("AT+CSQ", 3000);
  sendAT("AT+CREG?", 3000);
  sendAT("AT+CGATT=1", 10000);

  if (!sendAT("AT+SAPBR=3,1,\"Contype\",\"GPRS\"", SIM800_CMD_TIMEOUT_MS)) {
    Serial.println("SIM800: bearer profile failed");
    return false;
  }

  String apnCmd = String("AT+SAPBR=3,1,\"APN\",\"") + APN + "\"";
  if (!sendAT(apnCmd.c_str(), SIM800_CMD_TIMEOUT_MS)) {
    Serial.println("SIM800: APN setup failed");
    return false;
  }

  if (!sendAT("AT+SAPBR=1,1", 30000, "OK")) {
    Serial.println("SIM800: GPRS attach failed");
    return false;
  }

  sendAT("AT+SAPBR=2,1", 5000);
  Serial.println("SIM800L GPRS ready");
  return true;
}

bool httpGet(const String& url, String& bodyOut, int& httpCodeOut) {
  bodyOut = "";
  httpCodeOut = -1;

  sendAT("AT+HTTPTERM", 3000); // ignore result

  if (!sendAT("AT+HTTPINIT", SIM800_CMD_TIMEOUT_MS)) return false;
  if (!sendAT("AT+HTTPPARA=\"CID\",1", SIM800_CMD_TIMEOUT_MS)) return false;

  String urlCmd = "AT+HTTPPARA=\"URL\",\"" + url + "\"";
  if (!sendAT(urlCmd.c_str(), SIM800_CMD_TIMEOUT_MS)) {
    sendAT("AT+HTTPTERM", 3000);
    return false;
  }

  if (!sendAT("AT+HTTPACTION=0", 60000)) {
    sendAT("AT+HTTPTERM", 3000);
    return false;
  }

  String actionResponse = readSim800Until(60000, "+HTTPACTION:");
  int actionIndex = actionResponse.indexOf("+HTTPACTION:");
  if (actionIndex == -1) {
    sendAT("AT+HTTPTERM", 3000);
    return false;
  }

  int firstComma = actionResponse.indexOf(',', actionIndex);
  int secondComma = actionResponse.indexOf(',', firstComma + 1);
  int thirdComma = actionResponse.indexOf(',', secondComma + 1);

  if (firstComma == -1 || secondComma == -1) {
    sendAT("AT+HTTPTERM", 3000);
    return false;
  }

  httpCodeOut = actionResponse.substring(firstComma + 1, secondComma).toInt();

  if (httpCodeOut != 200) {
    sendAT("AT+HTTPTERM", 3000);
    return false;
  }

  sendAT("AT+HTTPREAD", SIM800_CMD_TIMEOUT_MS);
  String readResponse = readSim800Until(SIM800_CMD_TIMEOUT_MS, "+HTTPREAD:");

  int bodyStart = readResponse.indexOf("\r\n");
  if (bodyStart != -1) {
    bodyOut = readResponse.substring(bodyStart + 2);
    bodyOut.trim();
    int okPos = bodyOut.lastIndexOf("\r\nOK");
    if (okPos != -1) {
      bodyOut = bodyOut.substring(0, okPos);
    }
  }

  sendAT("AT+HTTPTERM", 3000);
  return true;
}

void applyRelayStateFromJson(const String& jsonBody) {
  StaticJsonDocument<250> doc;
  DeserializationError error = deserializeJson(doc, jsonBody);
  if (error) {
    Serial.print("JSON parse error: ");
    Serial.println(error.c_str());
    return;
  }

  const char* state = doc["state"];
  if (state == nullptr) return;

  relayOn = (String(state) != "OFF");
  digitalWrite(RELAY_PIN, relayOn ? HIGH : LOW);
  updateRelayLed();

  Serial.print("Relay set to ");
  Serial.println(relayOn ? "ON" : "OFF");
}

void pollServerStatus() {
  String body;
  int httpCode = -1;

  Serial.print("Polling ");
  Serial.println(serverUrl);

  if (httpGet(serverUrl, body, httpCode)) {
    serverConnected = true;
    updateConnectionLed();
    Serial.print("Response: ");
    Serial.println(body);
    applyRelayStateFromJson(body);
  } else {
    serverConnected = false;
    updateConnectionLed();
    Serial.print("HTTP failed, code=");
    Serial.println(httpCode);
  }
}

void setup() {
  Serial.begin(115200);
  delay(500);

  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, HIGH);
  relayOn = true;

  initLedStrip();

  preferences.begin("config", false);
  serverIP = preferences.getString("ip", "192.168.1.1");
  serverPort = preferences.getString("port", "65050");
  preferences.end();
  serverUrl = "http://" + serverIP + ":" + serverPort + "/status";

  Serial.println("READY");
  Serial.println("Loaded Target URL: " + serverUrl);

  sim800.begin(SIM800_BAUD, SERIAL_8N1, SIM800_RX, SIM800_TX);
  delay(1000);

  if (initSim800()) {
    pollServerStatus();
  } else {
    Serial.println("SIM800L init failed - check module, SIM, and APN");
    serverConnected = false;
    updateConnectionLed();
  }
}

void loop() {
  pollSerialConfig();

  if (millis() - lastRequestTime >= REQUEST_INTERVAL_MS) {
    lastRequestTime = millis();
    pollServerStatus();
  }
}
