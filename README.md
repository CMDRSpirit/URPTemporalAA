# Unity URP Temporal Anti-Aliasing
This is a Temporal Anti-Aliasing (TAA) solution for Unity's Universal render pipeline. URP does not have a TAA solution yet, so this may solve aliasing issues for devs using URP.

The implemetation is based up on the Siggraph2014 talk by Brian Karis:
High Quality Temporal Supersampling
https://de45xmedrsdbp.cloudfront.net/Resources/files/TemporalAA_small-59732822.pdf

# Comparison
![Anti-Aliasing comparison](https://github.com/CMDRSpirit/URPTemporalAA/blob/main/res/comp.png?raw=true)

# Usage
- Attach Temporal AA Camera script to your camera
- Add Temporal AA Feature to your renderer
- Done! 
