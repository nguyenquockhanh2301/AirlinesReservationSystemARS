# Cancellation System Flowchart

This document describes the cancellation process flow for the Airlines Reservation System (ARS).

## Overview

The cancellation system allows users to cancel their reservations with potential refunds based on cancellation timing. The system handles both single-flight and multi-leg reservations.

---

## Main Cancellation Flow

```
START
  |
  v
User accesses Cancel Page (/Refund/Cancel?reservationId=X)
  |
  v
[Authentication Check]
  |
  +---> Is User Logged In? --NO--> Redirect to Login
  |                                      |
  YES                                    v
  |                                Return URL stored for post-login redirect
  v
Retrieve Reservation from Database
  |
  +---> Include: User, Flight, OriginCity, DestinationCity, Payments
  |
  v
[Reservation Exists?]
  |
  +---> NO --> Return 404 Not Found
  |
  YES
  v
[Authorization Check]
  |
  +---> Is User Owner OR Admin? --NO--> Return 403 Forbidden
  |
  YES
  v
[Status Check]
  |
  +---> Is Status = "Cancelled"? --YES--> Show Info Message
  |                                        "Already Cancelled"
  NO                                             |
  |                                              v
  v                                     Redirect to Reservation Details
Calculate Days Before Departure
  |
  v
Calculate Refund Rate Based on Days:
  |
  +---> >= 30 days --> 100% refund (1.00)
  +---> >= 15 days --> 80% refund (0.80)
  +---> >= 7 days  --> 50% refund (0.50)
  +---> < 7 days   --> 0% refund (0.00)
  |
  v
Calculate Total Paid Amount
  |
  +---> Sum all Payments with Status = "Completed"
  |
  v
Calculate Potential Refund Amount
  |
  +---> Refund = Total Paid × Refund Rate
  +---> Round to 2 decimal places
  |
  v
Prepare RefundViewModel:
  |
  +---> Reservation Details
  +---> Days Before Departure
  +---> Potential Refund Amount
  +---> Refund Percentage
  |
  v
Display Cancel Confirmation Page
  |
  +---> Show flight details
  +---> Show refund policy
  +---> Show calculated refund amount
  +---> Show confirmation button
  |
  v
[User Decision]
  |
  +---> Cancel --> Return to Reservation Details
  |
  Confirm Cancellation
  |
  v
POST: /Refund/ConfirmCancel
  |
  v
[Re-authentication Check]
  |
  +---> Is User Logged In? --NO--> Redirect to Login
  |
  YES
  v
Retrieve Reservation (with Payments & Legs)
  |
  v
[Validation Checks]
  |
  +---> Reservation Exists? --NO--> Return 404
  +---> User Authorized? --NO--> Return 403
  +---> Already Cancelled? --YES--> Show Info & Redirect
  |
  ALL PASS
  v
Recalculate Refund Amount
  |
  +---> Use same refund rate logic
  |
  v
Create Refund Record
  |
  +---> RefundID (auto-generated)
  +---> ReservationID
  +---> RefundAmount
  +---> RefundDate (current DateTime)
  +---> RefundPercentage
  |
  v
Update Reservation Status to "Cancelled"
  |
  v
[Release Seats - Single Flight]
  |
  +---> Has FlightSeatId? --YES--> Call SeatService.CancelReservationSeatAsync()
  |                                      |
  NO                                     v
  |                                Mark FlightSeat as Available
  v                                Remove FlightSeat.ReservationID
[Release Seats - Multi-Leg]
  |
  +---> Has Legs? --YES--> For Each Leg:
  |                          |
  NO                         +---> Has FlightSeatId?
  |                          |           |
  v                          |          YES
Skip                         |           v
  |                          |     Call SeatService.CancelReservationSeatForLegAsync()
  |                          |           |
  |                          |           v
  |                          |     Mark FlightSeat as Available
  |                          |     Remove FlightSeat.ReservationLegID
  |                          |
  |                          v
  |                    Next Leg or Complete
  v
[Process Payment Refunds]
  |
  +---> Refund Amount > 0? --YES--> For each Completed Payment:
  |                                      |
  NO                                     v
  |                                Update TransactionStatus = "Refunded"
  v
Skip Payment Updates
  |
  v
Add Refund Record to Database
  |
  v
Save All Changes to Database
  |
  v
[Success Message]
  |
  +---> Refund > 0 --> "Reservation cancelled. A refund of $X.XX has been processed."
  +---> Refund = 0 --> "Reservation cancelled. No refund is due based on the cancellation policy."
  |
  v
Redirect to Reservation Details Page
  |
  v
END
```

---

## Seat Cancellation API Flow

### Single Reservation Seat Cancellation

```
START: POST /api/seat/cancel
  |
  v
Receive Request:
  {
    "reservationId": int
  }
  |
  v
Call SeatService.CancelReservationSeatAsync(reservationId)
  |
  v
Find FlightSeat WHERE ReservationID = reservationId
  |
  v
[FlightSeat Found?]
  |
  +---> NO --> Return 404
  |            { "cancelled": false, 
  |              "message": "No flight seat found for reservation" }
  YES
  v
Update FlightSeat:
  |
  +---> IsAvailable = true
  +---> ReservationID = null
  |
  v
Save Changes to Database
  |
  v
Return Success
  |
  +---> 200 OK
        { "cancelled": true }
  |
  v
END
```

### Multi-Leg Seat Cancellation

```
START: POST /api/seat/cancel/leg
  |
  v
Receive Request:
  {
    "reservationLegId": int
  }
  |
  v
Call SeatService.CancelReservationSeatForLegAsync(reservationLegId)
  |
  v
Find FlightSeat WHERE ReservationLegID = reservationLegId
  |
  v
[FlightSeat Found?]
  |
  +---> NO --> Return 404
  |            { "cancelled": false, 
  |              "message": "No flight seat found for reservation leg" }
  YES
  v
Update FlightSeat:
  |
  +---> IsAvailable = true
  +---> ReservationLegID = null
  |
  v
Save Changes to Database
  |
  v
Return Success
  |
  +---> 200 OK
        { "cancelled": true }
  |
  v
END
```

---

## Reschedule Cancellation Flow

```
START: POST /Reservation/CancelReschedule
  |
  v
Receive Parameters:
  |
  +---> reservationId: int
  +---> scheduleId: int? (optional)
  |
  v
Retrieve Reservation (with Payments)
  |
  v
[Reservation Exists?]
  |
  +---> NO --> Return 404 Not Found
  |
  YES
  v
[Check Reservation Status]
  |
  v
Is Status = "Rescheduled"?
  |
  +---> YES --> [Cancel Completed Reschedule]
  |              |
  |              v
  |         Find Pending Reschedule Payment
  |              |
  |              +---> PaymentMethod = "RescheduleDue"
  |              +---> TransactionStatus = "Pending"
  |              |
  |              v
  |         [Payment Found?]
  |              |
  |              +---> YES --> Remove Payment from Database
  |              |
  |              NO
  |              v
  |         Release Newly Reserved Seats
  |              |
  |              v
  |         Call SeatService.CancelReservationSeatAsync(reservationId)
  |              |
  |              v
  |         Update Reservation Status = "Confirmed"
  |              |
  |              v
  |         Save Changes
  |              |
  |              v
  |         Success Message:
  |         "Reschedule has been cancelled. The pending payment has been removed.
  |          Please contact support if you need to revert to your original flight details."
  |              |
  |              v
  |         Redirect to Reservation Details
  |              |
  |              v
  |         END
  |
  NO (In-Progress Reschedule)
  |
  v
Release Any Reserved Seats for Reschedule Attempt
  |
  v
Call SeatService.CancelReservationSeatAsync(reservationId)
  |
  v
Save Changes
  |
  v
Success Message:
"In-progress reschedule cancelled. Reserved seats have been released."
  |
  v
Redirect to Reservation Details
  |
  v
END
```

---

## Refund Policy

| Days Before Departure | Refund Percentage | Refund Rate |
|----------------------|-------------------|-------------|
| 30 days or more      | 100%             | 1.00        |
| 15-29 days           | 80%              | 0.80        |
| 7-14 days            | 50%              | 0.50        |
| Less than 7 days     | 0%               | 0.00        |

### Refund Calculation Formula

```
Total Paid = SUM(Payments WHERE TransactionStatus = "Completed")
Refund Rate = Based on Days Before Departure (see table above)
Refund Amount = ROUND(Total Paid × Refund Rate, 2)
```

---

## Database Changes During Cancellation

### Reservation Table
- **Status**: Updated from "Confirmed" → "Cancelled"

### Refund Table (New Record Created)
- **RefundID**: Auto-generated
- **ReservationID**: Links to cancelled reservation
- **RefundAmount**: Calculated refund amount
- **RefundDate**: Current DateTime
- **RefundPercentage**: Refund rate × 100

### Payment Table
- **TransactionStatus**: Updated from "Completed" → "Refunded" (if refund > 0)

### FlightSeat Table (Single Flight)
- **IsAvailable**: Updated from false → true
- **ReservationID**: Set to null

### FlightSeat Table (Multi-Leg)
- **IsAvailable**: Updated from false → true
- **ReservationLegID**: Set to null (for each leg)

---

## Access Control

### User Authorization Rules
1. **User must be logged in** - Otherwise redirected to login page
2. **User must be owner OR admin** - Otherwise returns 403 Forbidden
3. **Reservation must exist** - Otherwise returns 404 Not Found
4. **Reservation must not be already cancelled** - Otherwise shows info message

---

## Error Handling

### Common Error Scenarios

| Scenario | HTTP Status | Action |
|----------|-------------|--------|
| User not logged in | Redirect | Redirect to /Account/Login with return URL |
| Reservation not found | 404 | Return NotFound() |
| User not authorized | 403 | Return Forbid() |
| Already cancelled | Redirect | Show info message and redirect to details |
| No seats to release | N/A | Skip seat release step |

---

## Related Controllers & Services

### Controllers
- **RefundController**: Main cancellation logic (`Cancel`, `ConfirmCancel`)
- **SeatController**: Seat cancellation API endpoints
- **ReservationController**: Reschedule cancellation (`CancelReschedule`)

### Services
- **ISeatService**: Seat management interface
  - `CancelReservationSeatAsync(reservationId)`
  - `CancelReservationSeatForLegAsync(reservationLegId)`

### Models
- **Refund**: Stores refund information
- **Reservation**: Main reservation entity
- **Payment**: Payment records
- **FlightSeat**: Seat availability tracking
- **ReservationLeg**: Multi-leg journey segments

---

## Views

### Refund/Cancel.cshtml
- Displays cancellation confirmation page
- Shows flight details
- Shows refund policy and calculated refund amount
- Provides confirmation button

### Reservation/Details.cshtml
- Shows reservation details after cancellation
- Displays cancellation status
- Shows refund information

---

## Notes

1. **Seat Release**: Seats are immediately made available for other users upon cancellation
2. **Refund Processing**: Refunds are processed immediately but marked in the system (no actual payment gateway integration shown)
3. **Multi-Leg Support**: System handles both single-flight and multi-leg reservation cancellations
4. **Admin Access**: Admins can cancel any reservation, not just their own
5. **Audit Trail**: All refunds are recorded in the Refunds table for tracking purposes
6. **Reschedule Reversion**: When cancelling a completed reschedule, the system cannot automatically revert to original flight details (user must contact support)

---

## Future Enhancements

1. Store original reservation details before rescheduling to enable automatic reversion
2. Integrate with payment gateway for automatic refund processing
3. Email notifications for cancellation confirmations
4. Partial cancellations for multi-leg reservations
5. Cancellation fee structure (currently only time-based refund rates)
6. Waitlist management when seats become available
