# 🚑 QueueCure AI+

### Predictive Clinic Queue Intelligence Platform

> Transforming traditional clinic token systems into intelligent, real-time, predictive patient experiences.

---

# 📌 Problem Statement

76% of India's clinics still rely on paper tokens, manual calling, and uncertain waiting times.

Patients often wait 2–3 hours without knowing when they'll be called.

Receptionists manually manage queues, doctors have limited operational visibility, and clinics lack tools to predict delays, manage emergencies, or handle no-shows efficiently.

QueueCure AI+ solves this using real-time synchronization, predictive wait-time intelligence, virtual queueing, and clinic analytics.

---

# 🎯 Solution Overview

QueueCure AI+ is a real-time clinic queue intelligence platform that:

* Digitizes patient token management
* Predicts wait times using historical consultation data
* Provides dynamic arrival windows
* Handles emergency patients intelligently
* Detects consultation delays automatically
* Learns doctor consultation patterns
* Provides operational insights for clinics
* Synchronizes all screens instantly using SignalR

---
'''

# 🔄 Real-Time Event Flow

## Patient Registration Flow

Receptionist Registers Patient
│
▼
PatientAdded Event
│
▼
Patient Saved To Database
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
├──► Patient Tracker Created
│
└──► Analytics Dashboard Updated

---

## Call Next Patient Flow

Doctor Clicks "Call Next"
│
▼
QueueUpdated Event
│
▼
Current Token Updated
│
▼
Prediction Engine Recalculates Wait Times
│
▼
Arrival Windows Recalculated
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
├──► Patient Tracker Updated
│
└──► Analytics Dashboard Updated

---

## Emergency Queue Flow

Receptionist Marks Emergency Patient
│
▼
EmergencyInserted Event
│
▼
Emergency Patient Prioritized
│
▼
Queue Impact Analysis Generated
│
▼
Wait Times Recalculated
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
├──► Patient Tracker Updated
│
└──► Analytics Dashboard Updated

---

## Patient Skip Flow

Doctor Skips Patient
│
▼
PatientSkipped Event
│
▼
Patient Added To Recovery Queue
│
▼
Next Patient Selected
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
└──► Patient Tracker Updated

---

## Recovery Queue Flow

Receptionist Clicks Restore
│
▼
PatientRestored Event
│
▼
Patient Reinserted Into Queue
│
▼
Queue Recalculated
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
└──► Patient Tracker Updated

---

## Consultation Completion Flow

Doctor Completes Consultation
│
▼
ConsultationCompleted Event
│
▼
Consultation History Saved
│
▼
Historical Learning Data Updated
│
▼
Doctor Statistics Updated
│
▼
Prediction Model Updated
│
▼
SignalR Queue Hub
│
├──► Queue Updated
│
├──► Analytics Updated
│
└──► Doctor Metrics Updated

---

## Smart Delay Detection Flow

Consultation Running Longer Than Expected
│
▼
DelayDetected Event
│
▼
Delay Logged
│
▼
Queue Reliability Recalculated
│
▼
Arrival Windows Recalculated
│
▼
Wait Time Predictions Updated
│
▼
SignalR Queue Hub
│
├──► Receptionist Dashboard Updated
│
├──► Doctor Console Updated
│
├──► TV Display Updated
│
├──► Patient Tracker Updated
│
└──► Analytics Dashboard Updated

---

## AI Prediction Flow

Queue State Changes
│
▼
Prediction Engine Triggered
│
▼
Historical Data Analyzed
│
▼
Wait Time Predicted
│
▼
Confidence Score Generated
│
▼
Explanation Generated
│
▼
PredictionUpdated Event
│
▼
SignalR Queue Hub
│
└──► All Connected Clients Updated

---

## WhatsApp Notification Flow

Queue Event Occurs
│
▼
Notification Service Triggered
│
▼
Message Generated
│
▼
WhatsApp Outbox Updated
│
▼
Patient Notification Logged

Examples:
• Registration Confirmation
• Arrival Reminder
• Called Notification
• Skip Notification
• Restore Notification

'''

---

# 🏥 Core Modules

## 1️⃣ Receptionist Dashboard

### Features

* Walk-In Patient Registration
* Patient Name & Phone Capture
* Visit Category Selection
* Automatic Token Generation
* Emergency Priority Check-In
* Queue Management
* Recovery Queue for No-Shows
* Active Doctor Monitoring Grid
* WhatsApp Notification Outbox
* Queue Health Monitoring

### Receptionist Benefits

* Faster registration
* Reduced manual effort
* Real-time queue visibility
* Mistake-proof workflow

---

## 2️⃣ Patient Live Tracker

Patients can access their queue status using a token-specific tracking URL.

### Live Information

* Current Token Being Served
* Assigned Doctor
* Assigned Room
* Tokens Ahead
* Estimated Wait Time
* Confidence Score
* Queue Reliability Score

---

## 🎫 Virtual Queue Passport

Instead of forcing patients to sit inside the clinic for hours, QueueCure AI+ provides:

### Dynamic Arrival Windows

Example:

Recommended Arrival Window:
10:30 AM – 10:45 AM

### Smart States

* Relax
* Prepare to Travel
* Arrive Soon
* Arrive Now
* Running Late

This significantly reduces unnecessary waiting room congestion.

---

## 3️⃣ Doctor Console

Doctors can manage consultations directly.

### Features

* Call Next Patient
* Start Consultation
* Complete Consultation
* Skip Absent Patient
* View Queue Status
* Consultation Timer
* Delay Monitoring

### Safety Rules

A new patient cannot be called until the current consultation is completed or skipped.

---

## 4️⃣ TV Lobby Display

A full-screen waiting room display.

### Displays

* Now Serving Tokens
* Doctor Name
* Room Number
* Upcoming Queue
* Real-Time Updates

### Extra Features

* Auto-refresh via SignalR
* Audio Chime Notification
* Large-screen optimized UI

---

# 🤖 AI & Predictive Intelligence

## Smart Wait-Time Prediction Engine

Unlike traditional systems that use fixed averages:

Wait Time = Queue Length × Average Time

QueueCure AI+ uses historical consultation data.

### Factors Used

* Doctor
* Patient Category
* Queue Length
* Day of Week
* Hour of Day
* Historical Consultation Duration
* Emergency Interruptions

### Output

Estimated Wait:
34 Minutes

Confidence:
91%

---

## Explainable AI

Every prediction explains itself.

Example:

Estimated Wait:
34 Minutes

Reason:

* 4 patients ahead
* Doctor average consultation time: 7 mins
* 1 emergency patient in queue
* Historical Monday traffic increase
* Doctor currently running 5 mins behind

This improves trust and transparency.

---

## Continuous Learning Engine

Every completed consultation updates the prediction system.

Captured Data:

* Consultation Duration
* Actual Wait Time
* Queue Position
* Patient Category
* Doctor Information

The system continuously improves prediction accuracy over time.

---

## Doctor Pace Learning

The platform learns individual doctor consultation behavior.

Example:

Dr. Kumar
Average Consultation: 6 mins

Dr. Priya
Average Consultation: 14 mins

Predictions automatically adapt to each doctor's pace.

---

# 🚨 Advanced Queue Intelligence

## Emergency Queue Handling

Receptionists can elevate critical patients.

Features:

* Emergency Priority Insertion
* Automatic Queue Recalculation
* Real-Time Notifications
* Full Audit Trail

---

## Recovery Queue

Handles no-show patients safely.

Workflow:

* Patient Skipped
* Added to Recovery Queue
* Restore with One Click
* Fair Queue Reintegration

---

## Smart Delay Detection

Detects consultations running significantly longer than expected.

Example:

Expected Duration:
10 mins

Current Duration:
18 mins

Delay Detected:
+8 mins

Actions:

* Recalculate Wait Times
* Update Arrival Windows
* Notify Connected Screens
* Log Delay Events

---

## Queue Impact Analysis

Whenever an emergency patient or delay occurs:

QueueCure AI+ calculates impact.

Example:

Emergency Patient Added

Impact:

Token 105 → +6 mins
Token 106 → +5 mins
Token 107 → +4 mins

Patients receive updated estimates instantly.

---

## Queue Reliability Score

Measures prediction trustworthiness.

Example:

Queue Reliability:
94 / 100

Status:
Excellent

Calculated Using:

* Prediction Accuracy
* Doctor Consistency
* Queue Congestion
* Emergency Frequency
* Delay Events

---

# 📊 Clinic Analytics Dashboard

Provides operational insights.

### Metrics

* Patients Served Today
* Average Wait Time
* Average Consultation Duration
* Prediction Accuracy
* Queue Reliability
* Doctor Utilization
* Doctor Idle Time
* No Show Rate
* Emergency Rate

---

# 🔄 Real-Time Communication

Built using SignalR WebSockets.

Connected Screens:

* Receptionist Dashboard
* Doctor Console
* Patient Tracker
* TV Lobby Display
* Analytics Dashboard

Any action updates every connected screen instantly.

---

# 🔐 Security

Authentication:

* JWT Bearer Authentication

Authorization:

* Role-Based Access Control

Roles:

### Receptionist

* Register Patients
* Manage Queue
* Handle Recovery Queue

### Doctor

* Manage Consultations
* Call Patients
* Complete Consultations

---

# ⚙️ Technology Stack

### Frontend

* HTML
* CSS
* JavaScript

### Backend

* ASP.NET Core Web API
* SignalR

### Database

* SQL Server
* Entity Framework Core

### Security

* JWT Authentication

### Analytics

* Statistical Learning Engine
* Predictive Queue Intelligence

---

# 🧠 Concurrency & Edge Cases

Handled Scenarios:

* Simultaneous Call Next Actions
* Emergency Queue Insertion
* Consultation Delays
* Patient No-Shows
* Recovery Queue Restoration
* Network Reconnection
* Multiple Receptionists
* Multiple Doctors

Solutions:

* Database Transactions
* Queue Locking
* SignalR Synchronization
* Optimistic Concurrency Control

---

# 🚀 Innovation Highlights

✅ Virtual Queue Passport

✅ Explainable AI

✅ Doctor Pace Learning

✅ Smart Delay Detection

✅ Queue Reliability Score

✅ Recovery Queue

✅ Queue Impact Analysis

✅ Continuous Learning Engine

✅ Real-Time Multi-Screen Synchronization

---

# 🎥 Demo

Demo Video:
[Add Video Link]

Live Prototype:
[Add Prototype Link]

---

# 📂 Repository

GitHub Repository:
[Add Repository Link]

---

# 👥 Team

Team Name:
Penguin?

Members:

* Devendiran K

---

# 💡 Vision

Most queue systems tell patients:

"How long you will wait."

QueueCure AI+ tells patients:

"When you should actually arrive."

By combining real-time synchronization, predictive intelligence, and operational analytics, QueueCure AI+ transforms clinic queues from reactive waiting systems into proactive patient experiences.
