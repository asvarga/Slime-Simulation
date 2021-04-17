



# TODO

- Conflict responses (see notes below)
    - do this in a new branch
- message dispersal example
- blur

# LATER

- Optimize
- Frog and Pixels may update different number of times per frame

# NOTES

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


