// Two-joystick controller for Shadows of Chandni.
// SAME pins and SAME serial format as sketch_nov17a ("lx,ly,lb,rx,ry,rb\n" at 115200), so Unity needs no change.
//
// What is different: every analog axis is given time to settle before it is sampled.
// The Uno has ONE analog-to-digital converter that is switched between pins. Reading A1 and then A2 straight away
// leaves a trace of A1's voltage on A2's sample, so moving the left stick nudged the right stick's reading
// (and A2 -> A3 the same way). Fix: throw away the first reading after switching pins, wait a moment, then average a few.

const int LEFT_X = A0;
const int LEFT_Y = A1;
const int LEFT_BUTTON = 2;

const int RIGHT_X = A2;
const int RIGHT_Y = A3;
const int RIGHT_BUTTON = 3;

const int SAMPLES = 4;

int readAxis(int pin) {
    analogRead(pin);                 // throw-away: the converter's input has just been switched to this pin
    delayMicroseconds(150);          // let its sample-and-hold capacitor charge through the joystick's resistance
    long sum = 0;
    for (int i = 0; i < SAMPLES; i++) {
        sum += analogRead(pin);
        delayMicroseconds(50);
    }
    return (int)(sum / SAMPLES);
}

void setup() {
    Serial.begin(115200);

    pinMode(LEFT_BUTTON, INPUT_PULLUP);
    pinMode(RIGHT_BUTTON, INPUT_PULLUP);
}

void loop() {
    int lx = readAxis(LEFT_X);
    int ly = readAxis(LEFT_Y);
    int lb = digitalRead(LEFT_BUTTON);

    int rx = readAxis(RIGHT_X);
    int ry = readAxis(RIGHT_Y);
    int rb = digitalRead(RIGHT_BUTTON);

    Serial.print(lx);
    Serial.print(",");
    Serial.print(ly);
    Serial.print(",");
    Serial.print(lb);
    Serial.print(",");
    Serial.print(rx);
    Serial.print(",");
    Serial.print(ry);
    Serial.print(",");
    Serial.println(rb);

    delay(16);       // about 50 updates per second including the settling time above
}
