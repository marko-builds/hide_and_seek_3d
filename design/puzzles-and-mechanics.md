### **1. Game Mechanics (Core Systems)**

- **The "Noise Radius" System:**

  - Instead of just "being heard," give the player a visible ring (only visible to the player) that expands or shrinks based on movement speed (crouch vs. sprint).

  - **The Twist:** Surfaces change this. Carpet shrinks the radius; puddles or broken glass make it pulse loudly.

- **Light-Based Detection (The "Exposure" Meter):**

  - Since it’s 3D, use a meter that fills up based on the intensity of light hitting the player.

  - **The Mechanic:** Standing in deep shadow makes you 100% invisible even if an enemy is looking your way, but holding a "Glow Gem" (currency/quest item) makes you a walking target.

- **Temperature/Scent Trails:**

  - Some enemies don't "see" well but follow "trails." If the player stays in one spot too long, they leave a "scent" (a hidden timer/trail).

  - **Counter-play:** The player can interact with a "Fan" or "Air Vent" to blow their scent in a different direction to mislead the seeker.


### **2. Puzzle Mechanics (The "Logic" of the World)**

- **Weight-Shifting Platforms:**

  - Use Unity's physics to create a large teeter-totter bridge. To cross, you might need to throw multiple "distractables" (heavy rocks) to one side to weigh it down, or bait a heavy enemy into standing on one side while you sneak across the raised end.

- **The "Shadow Puppet" Puzzle:**

  - A door only opens when a specific shadow shape is cast on a wall sensor. The player must move furniture or rotate statues (interactables) to align their shadows perfectly.

- **Relay Power Loops:**

  - You have switches and gems. Imagine a "Conductive Gem." To power a door, you must place these gems in pedestals to bridge a gap in an electrical circuit.

  - **Stealth Twist:** The Seeker is attracted to the humming sound of the electricity, so you have to time your "power-ups" for when the Seeker is far away.


### **3. Specific Puzzle Designs**

#### **Puzzle A: The "Blind Musician" (Sound Manipulation)**

- **Setup:** A room full of "Wind Chime" interactables and a blind, hyper-sensitive Seeker.

- **The Goal:** Reach a key on the far side.

- **Solution:** The player can't walk across the creaky floor. They must throw a distractable at a distant wind chime. While the chime is ringing, its loud noise "deafens" the Seeker, allowing the player to sprint across the creaky floor for a few seconds.

#### **Puzzle B: The "Mirror Maze" (Line of Sight)**

- **Setup:** A room filled with tall, moveable mirrors.

- **The Goal:** Cross a room watched by a Seeker with a very long, narrow field of vision.

- **Solution:** The player pushes the mirrors into positions where the Seeker ends up looking at their own reflection or a wall. Because it's top-down, the player can see the "Vision Cone" bouncing off the mirrors, making it a spatial geometry puzzle.

#### **Puzzle C: The "Greedy Guard" (Lure Logic)**

- **Setup:** A Seeker who prioritizes "Gems" (your currency) over the player.

- **The Goal:** Get the Seeker to move away from a pressure plate they are guarding.

- **Solution:** The player must "spend" some of their collected currency by dropping gems in a trail leading into a prison cell. Once the Seeker enters to pick them up, the player flips a switch to lock the door. You trade wealth for safety.


### **4. Advanced Interaction: The "Ghost" Mechanic**

If your game has a supernatural or high-tech theme, give the player a **"Phantom Projection."**

- The player can "interact" with a statue to leave a ghost-image of themselves.

- The Seeker will chase the ghost-image.

- **Puzzle:** You must project your ghost through a "Detection Laser" (which triggers an alarm) while the real you sneaks through a side door that only opens when the alarm is active.


### **Implementation Tip: "Telegraphing"**

In a puzzle-stealth game, the player needs to know *why* they failed.

- When a Seeker hears a sound, put a **"?" icon** over their head.

- When they see the player, use a **"!" icon**.

- In Unity, you can use **Gizmos** in the editor to visualize these detection ranges while you are designing the levels!

