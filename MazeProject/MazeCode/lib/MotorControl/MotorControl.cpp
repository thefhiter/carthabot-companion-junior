// MotorControl.cpp
#include "MotorControl.h"
#include "Arduino.h"
#include "../communs.h"


MotorControl::MotorControl(){

}

void MotorControl::Init(){

  // Set all the motor control inputs to OUTPUT
  pinMode(MOT_1_PIN, OUTPUT);
  pinMode(MOT_2_PIN, OUTPUT);
  pinMode(MOT_1_ADC_PIN, OUTPUT);
  pinMode(MOT_2_ADC_PIN, OUTPUT);

  // Turn off motors - Initial state
  digitalWrite(MOT_1_PIN, LOW);
  digitalWrite(MOT_2_PIN, LOW);
  digitalWrite(MOT_1_ADC_PIN, LOW);
  digitalWrite(MOT_2_ADC_PIN, LOW);

}

void MotorControl::GoForward(){

    digitalWrite(MOT_1_PIN, LOW);
    analogWrite(MOT_1_ADC_PIN, speed);

    digitalWrite(MOT_2_PIN, HIGH);
    analogWrite(MOT_2_ADC_PIN, speed);
    delay(1000);
}
void MotorControl::GoBack(){
    digitalWrite(MOT_1_PIN, HIGH);
    analogWrite(MOT_1_ADC_PIN, speed);

    digitalWrite(MOT_2_PIN, LOW);
    analogWrite(MOT_2_ADC_PIN, speed);
    delay(1000);

}
void MotorControl::TurnFullLeft() {
    digitalWrite(MOT_1_PIN, HIGH);
    analogWrite(MOT_1_ADC_PIN, speed);

    digitalWrite(MOT_2_PIN, HIGH);
    analogWrite(MOT_2_ADC_PIN, speed);
    delay(1000);

}
void MotorControl::TurnFullRight() {
    digitalWrite(MOT_1_PIN, LOW);
    analogWrite(MOT_1_ADC_PIN, speed);

    digitalWrite(MOT_2_PIN, LOW);
    analogWrite(MOT_2_ADC_PIN, speed);
    delay(1000);

}
void MotorControl::Stop() {
    digitalWrite(MOT_1_PIN, LOW);
    digitalWrite(MOT_2_PIN, LOW);

    analogWrite(MOT_1_ADC_PIN, LOW);
    analogWrite(MOT_2_ADC_PIN, LOW);
}
void MotorControl::TurnLeft(){



    /*digitalWrite(MOT_A1_PIN, LOW);
    analogWrite(MOT_A2_PIN, speed);
    digitalWrite(MOT_B1_PIN, LOW);
    analogWrite(MOT_B2_PIN, speed/2);*/
}
void MotorControl::TurnRight(){
   /*digitalWrite(MOT_A1_PIN, LOW);
    analogWrite(MOT_A2_PIN, speed/2);
    digitalWrite(MOT_B1_PIN, LOW);
    analogWrite(MOT_B2_PIN, speed);*/
}

void MotorControl::TurnRightRight(){
   /* digitalWrite(MOT_A1_PIN, LOW);
    analogWrite(MOT_A2_PIN, LOW);
    digitalWrite(MOT_B1_PIN, 80);
    analogWrite(MOT_B2_PIN, 80);
*/
}
void MotorControl::TurnLeftLeft(){
    /*digitalWrite(MOT_A1_PIN, 80);
    analogWrite(MOT_A2_PIN, 80);
    digitalWrite(MOT_B1_PIN, LOW);
    analogWrite(MOT_B2_PIN, LOW);
*/
}

