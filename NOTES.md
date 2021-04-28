



# TODO

- move to new repo
- write some better random methods
    - note that currently with multiple steps per frame, they'll all give the same random values
- message passing (with commutative dynamical types)
- memo dispersal example
- birth+death
- environment interaction (pheromones or turing tape?)
- USER code

# LATER

- Frog and Pixels may update different number of times per frame
- make it fun

# OPTIMIZE

- change `numthreads` in kernels
- data representation
    - use smaller ints where possible
    - use int ind instead of int2 pos

# NOTES

## Message Passing

- Let the user define a message type
    - Fields should have commutative monoidal types to avoid race conditions
    - i.e. they should have a type, a default?, and a single comm+assoc binary operation
    - structs/tuples work by combining contents element-wise
    - the interpreter language will provide all legal types
        - ((u)int, 0, +)
        - ((u)int, 0, |)
        - ((u)int, 0, &)
        - ((?, uint), (?, 0), (:=, inc))   // set value with conflicts resolving to default
        - structs/tuples
    - these may also have Readout functions
- During Act, the user sends Update messages to these fields
    - this does `value := Update(value, msg)`
- During Resolve, the system may apply a Readout function before sending to agent
    - ex: for the simple conflict resolution
- Note that these fields act as little actors
    - alternatively, discrete dynamical systems: https://youtu.be/8T-Km3taNko?t=873
        - but with S == I, and with commutative Update 

## Conflict Resolution

- Conflict responses: **ignore the below strategy**
    - keep the counter
    - every frog checks the counter before moving themselves
    - return the count to each frog as the status
        - this could be useful for message dispersal
    - this mainly only requires moving resolve logic from pixel -> frog

- Conflict responses; use the fancy InterlockedExchange approach in FrogAct
    - make sure PixelMail.occupant is set to -1 every step
    - in FrogAct, do an InterlockedExchange
        - in PixelMail?     (?)
        - in Pixel?         (?)
    - if result != -1
        - send conflict message to 
            - other FrogMail?   (*)
            - own FrogMail?     (*)
            - PixelMail?        ( )
    - in FrogResolve
        - if conflict
            - pass along conflict info
        - else
            - if still there    ( )
                - update self+pixels (*)
            - else
                - pass along conflict info
    - in PixelResolve
        - update pixels? ( )
- we can either
    - resolve conflicts in FrogResolve  (*)
    - resolve conflicts in PixelResolve ( )
    - get rid of the frogs buffer, and store frogs in pixels
        - this is probaby never sane

- *We really want to do/store as much as possible per-frog, not per-pixel*
- Should pixels even know their occupant?   (*)
    - if we want message passing
- yes, but should pixelMail?                (?)
    - this is where the conflicts are resolved
- do we even need pixelMail?                (?)
    - or can all updates be done in FrogAct?
    - *maybe we should assume all Acts and all Resolves run in parallel?*
    - *or maybe only do official updates to data buffers?*
        - relying on frogResolve to undo bad changes is gross

- Is it possible to send conflict message to just a single frogMail? (defer)
    - could just send message to self, and check if still in square
    - this is complicated and may not even be faster

##