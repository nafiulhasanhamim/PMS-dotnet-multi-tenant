# 🛒 Design Pattern Playground: In-Memory Checkout Engine

This project is designed to be a "Pattern Gym" — a place where complexity is artificially introduced or architecturally preferred to verify and learn Design Patterns.

## 🌟 Core Concept
An **In-Memory Checkout Engine** that handles a shopping cart's lifecycle from item selection to final order placement. Because it is in-memory, we ignore database complexity and focus purely on **Object-Oriented Design** and **Behavioral Patterns**.

## 🏗️ Architectural Blueprint

### 1. The Checkout Pipeline (Chain of Responsibility)
**Goal:** Process an order through a series of strict steps where each step can decide to stop the process or pass it along.

*   **Pattern:** **Chain of Responsibility**
*   **The Steps (Handlers):**
    1.  `StockValidationHandler`: Do we have enough items?
    2.  `RegionAvailabilityHandler`: Can we ship to this country?
    3.  `RiskAssessmentHandler`: Does this look like fraud?
    4.  `TaxCalculationHandler`: Calculate taxes based on origin/destination.
    5.  `InventoryReservationHandler`: Temporarily hold the items.

### 2. Pricing & Discounts (Strategy & Decorator)
**Goal:** Calculate the final price dynamically based on complex rules.

*   **Pattern:** **Strategy** (For the base pricing logic)
    *   `RegularMemberPricingStrategy`
    *   `VIPMemberPricingStrategy` (Flat 10% off)
    *   `HolidayPricingStrategy` (Time-based logic)
*   **Pattern:** **Decorator** (For strict add-ons to individual items)
    *   `GiftWrappingDecorator`: Wraps a `CartItem` and adds $5.
    *   `InsuranceDecorator`: Wraps a `CartItem` and adds 2% cost.
    *   *Usage*: `new InsuranceDecorator(new GiftWrappingDecorator(item))`

### 3. Payment Processing (Adapter & Facade)
**Goal:** Simulate talking to different external providers (Stripe, PayPal, Crypto) uniformly.

*   **Pattern:** **Adapter**
    *   `StripeAdapter`: Adapts Stripe's specific API to your `IPaymentProvider`.
    *   `PayPalAdapter`: Adapts PayPal's specific API.
*   **Pattern:** **Facade**
    *   `PaymentProcessingFacade`: A simple entry point for the rest of the app (`ProcessPayment(amount, method)`) that hides the complexity of choosing the right adapter, retrying on failure, and logging transactions.

### 4. Order Lifecycle (State)
**Goal:** Manage the transitions of an order, ensuring you can't ship an unpaid order or cancel a delivered one.

*   **Pattern:** **State**
    *   Context: `Order`
    *   States: `NewState`, `PendingPaymentState`, `PaidState`, `ShippedState`, `CancelledState`, `RefundedState`.
    *   *Logic*: Each state class implements methods like `Pay()`, `Ship()`, `Cancel()`. If you call `Ship()` on `NewState`, it throws an application error.

### 5. Shopping Cart Bundles (Composite)
**Goal:** Treat a "Bundle of Items" exactly like a single "Item".

*   **Pattern:** **Composite**
    *   Component: `ICartItem` (get price, get weight)
    *   Leaf: `ProductItem` (A simple book or laptop)
    *   Composite: `ProductBundle` (Contains list of `ICartItem`).
    *   *Power*: You can put a Bundle inside a Bundle, and the price calculation (`GetPrice()`) works recursively automatically.

### 6. Notifications (Observer)
**Goal:** When an order is finally placed, notify 5 different disparate systems without tightly coupling them.

*   **Pattern:** **Observer** (or **MediatR** style Pub/Sub)
    *   Subject: `Order`
    *   Observers:
        *   `EmailService`: Sends receipt.
        *   `InventorySystem`: Decrements stock permanently.
        *   `AnalyticsService`: Tracks "Items sold".
        *   `MarketingService`: Checks if user earned a badge.

### 7. Actions & Undo (Command)
**Goal:** Allow the user to "Undo" adding an item or applying a coupon.

*   **Pattern:** **Command**
    *   `AddToCartCommand` (Stores the item added)
    *   `ApplyCouponCommand` (Stores the code applied)
    *   *Power*: Maintain a `Stack<ICommand>` history. `Undo()` simply pops the command and calls the command's `Undo()` method.

## 🚀 Scenario: "The Complex Check-out"
Here is a user story to test your design:

> "A **VIP User** adds a **Bundle** (Laptop + Mouse) and a separate **Book**. They add **Gift Wrapping** to the Book only. They apply a **'SUMMER2024' coupon**. They pay using **PayPal**. Once paid, the system alerts the Warehouse to ship and emails the user."

### Implementation Checklist
1.  [ ] **Domain Entities**: `Product`, `Order`, `User`.
2.  [ ] **Composite**: `ProductBundle`.
3.  [ ] **Decorators**: `CartItemDecorator`.
4.  [ ] **Chain**: `CheckoutPipeline`.
5.  [ ] **Strategies**: `IPricingStrategy`.
6.  [ ] **State**: `OrderState`.
7.  [ ] **Command**: `CartCommand`.


## 📚 Detailed User Stories

These stories are designed to force the usage of specific design patterns.

### Story 1: "The VIP Bundle Purchase" (Primary Scenario)
**Patterns:** `Composite`, `Strategy`, `Decorator`, `Adapter`, `State`, `Observer`.

> **As a** VIP Member,
> **I want to** buy a "Gamer Bundle" (Laptop + Mouse) and a specific Book with Gift Wrapping,
> **So that** I can get my 10% VIP discount on the base price, but pay full price for the Gift Wrapping.
>
> **Acceptance Criteria:**
> 1.  System identifies User as `VIP` -> Applies `VIPMemberPricingStrategy` (10% off items).
> 2.  User adds generic `ProductBundle` ("Gamer Set"). Price is sum of children.
> 3.  User adds `Product` ("Design Patterns Book").
> 4.  User wraps the Book -> `GiftWrappingDecorator` sums $5.00 *after* discount logic.
> 5.  Checkout Pipeline runs:
>     *   Stock Check: Pass.
>     *   Risk Check: Pass (< $5000).
>     *   Tax: Calculated based on Destination.
> 6.  Payment: User selects `PayPal` -> `PayPalAdapter` processes simulated transaction.
> 7.  Post-Purchase:
>     *   Order State moves from `PendingPayment` to `Paid`.
>     *   `EmailService` gets notified to send receipt.

---

### Story 2: "The Indecisive Shopper"
**Patterns:** `Command`, `State`.

> **As an** Indecisive Shopper,
> **I want to** add items to my cart and then undo my last 3 actions,
> **So that** I don't have to manually remove items one by one.
>
> **Acceptance Criteria:**
> 1.  User adds Item A.
> 2.  User applies Coupon "SAVE10".
> 3.  User adds Item B.
> 4.  User clicks "Undo" -> Item B is removed.
> 5.  User clicks "Undo" -> Coupon "SAVE10" is removed.
> 6.  User clicks "Undo" -> Item A is removed. Cart is empty.

---

### Story 3: "The International Shipping Failure"
**Patterns:** `Chain of Responsibility`, `State`.

> **As a** System Administrator,
> **I want** the system to block shipments to "Mars",
> **So that** we don't lose money on impossible logistics.
>
> **Acceptance Criteria:**
> 1.  User proceeds to checkout with address "Mars Colony 1".
> 2.  `StockValidationHandler` passes.
> 3.  `RegionAvailabilityHandler` **fails** (Mars is not in allowed regions).
> 4.  Checkout process aborts immediately.
> 5.  Order stays in `New` state (does not advance to `PendingPayment`).
> 6.  User sees error: "Shipping to Mars is not supported."

---

### Story 4: "The High-Risk Order"
**Patterns:** `Chain of Responsibility`, `State`.

> **As a** Risk Manager,
> **I want** orders over $10,000 to be automatically flagged for manual review,
> **So that** we prevent credit card fraud.
>
> **Acceptance Criteria:**
> 1.  User adds 5 Laptops ($15,000 Total).
> 2.  `RiskAssessmentHandler` detects Amount > $10,000.
> 3.  Handler stops the pipeline *but* does not throw error.
> 4.  Order State transitions to `AwaitingReviewState` (Not `PendingPayment`).
> 5.  User sees message: "Your order is under review."
