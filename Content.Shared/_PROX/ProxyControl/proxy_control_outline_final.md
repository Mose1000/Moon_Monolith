# Proxy Control System — Final Architecture & Feature Outline

This document provides a comprehensive, up-to-date overview of the **Proxy Control** feature. This system allows players to remotely operate cyborgs (proxies) from a safe location within a specialized pod, simulating a neural uplink.

---

## 1. Core Hardware & Mechanics

### 1.1 The Neural Link Pod (`proxy_control_pod`)
The **Neural Link Pod** is the operator's interface and sanctuary.
- **Physical Integration**: Acts as an airtight container (`BodyContainerId`) that completely seals the operator inside, protecting them from atmospheric hazards and preventing movement-based desyncs while linked.
- **Entry Mechanics**: Players enter the pod via Drag-and-Drop, or by using the priority `Alt-Click` (Enter) verb. 
- **ID Validation Requirement**: The pod requires an ID card inserted into its dedicated `IdCardSlot` *prior* to entry. The system dynamically copies this exact ID Card's **Name** and **Access Tags** directly to the controlled cyborg, heavily restricting or expanding the cyborg's capabilities depending on the operator's credentials.
- **Power Stability**: Equipped with an internal battery to maintain the neural link during minor local grid/APC power fluctuations.

### 1.2 The Proxy Control Unit (`proxy_control_unit`)
A specialized modular brain substitute that bridges the gap between the pod and a cyborg chassis.
- **Linking**: A player uses the unit directly on a Neural Link Pod to encode a permanent network link between the two.
- **Installation**: The encoded unit is inserted into any standard **Cyborg Chassis** brain slot. It inherently possesses the `BorgBrainComponent` to satisfy engine whitelists, but utilizes custom bypass logic to prevent standard mind-transfers.
- **Activation**: Once installed, it grants the chassis the `ProxyControllableComponent`, instantly registering the proxy as available to the linked Pod's UI network.

---

## 2. Remote Operation & User Flow

1. **Deployment**: An operator links a Control Unit to a Pod and installs it into a Cyborg Chassis.
2. **Setup**: The operator inserts their ID card into the Pod.
3. **Embark**: The operator physically enters the Pod.
4. **Selection**: A custom UI window (`ProxyControlPodUiKey`) automatically opens, displaying a list of all proxies successfully linked to the pod. It details their specific names, health conditions (Alive/Dead), and availability (Occupied).
5. **Synchronization**: Selecting a valid proxy initiates a `3-second DoAfter` sequence.
6. **Execution**: Upon completion, the engine's `Mind.Visit()` mechanism physically suspends the player's viewport from their biological body and injects it into the Cyborg Chassis.
7. **Simulation**: The proxy inherits the operator's ID Card name and explicit access combinations, overriding its default silicon protocols. The operator controls the proxy natively.

---

## 3. Link Severance & Fail-Safes

To ensure extreme stability, the system automatically intercepts various catastrophic and voluntary edge cases to gracefully tear down the neural link:

- **Voluntary Disconnection**: The operator is granted a custom `ActionProxyDisconnect` action (button) in their active toolbar. Clicking this immediately un-visits the proxy, restoring the player to the pod.
- **ID Card Ejection**: If a bystander removes the ID card from the Neural Link Pod while a link is active, the system considers the authorization revoked and violently severs the connection, sending the operator back to their body.
- **Pod Ejection**: Dragging the operator out of the Pod, or using the priority `Alt-Click` (Eject) verb, snaps the link and restores standard physics.
- **Module Removal Exemption**: If the Proxy Control Unit is ripped out of the Cyborg Chassis (even by the operator themselves), the engine intercepts the removal, dynamically prevents standard brain-mind theft, strictly severs the network link, and returns the operator to the pod untouched.
- **Proxy Destruction (Feedback)**: If the Cyborg Chassis is destroyed (MobState becomes Dead) while actively controlled, the link shatters. The operator is instantly returned to their body inside the pod and suffers a calculated `Shock/Stun` neural feedback damage penalty.
- **Total Power Failure**: If the Pod loses both grid power and exhausts its internal battery reserves, the connection drops immediately. Local distance checks also run periodically, terminating the link if the proxy wanders outside the pod's configured broadcast threshold.

---

## 4. State Restoration

When a link is severed via any of the above fail-safes:
1. The operator's Mind safely executes `UnVisit()` to return to the biological body.
2. The custom `DisconnectAction` is revoked from the proxy chassis.
3. The Cyborg Chassis's original factory identity (Name) is restored.
4. The Cyborg Chassis's default generic silicon `AccessTags` are completely reinstated, overwriting the temporary ID Card borrowing. 
5. The `ProxyOperatorComponent` is cleanly stripped from the player entity.
