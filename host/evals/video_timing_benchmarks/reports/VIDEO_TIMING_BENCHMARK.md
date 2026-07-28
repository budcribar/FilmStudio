# Video Timing Benchmark Report (Estimate vs. Actual)

**Execution Date:** 2026-07-28 17:13:22 UTC  
**Video Model Tested:** `fal-ai/hunyuan-video` (`Fal`)  
**Benchmark Count:** 10 / 35 total categories  

| Category ID | Category | Mode | Gamma (γ) | Action Prompt | Estimated Overhead | Actual Measured Overhead | Delta |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `cam_push_in` | Camera Movement | `serial` | `0.00` | Cinematic slow push-in zoom on a character standing in a dimly lit room. | 1.8s | **1.8s** | +0.0s |
| `react_gasp_shock` | Facial Reaction | `serial` | `0.00` | Character gasps in shock, eyes widening in sudden realization. | 1.5s | **1.3s** | -0.2s |
| `act_heavy_carry` | Physical Action | `serial` | `0.00` | Strong man carrying six heavy grocery bags up a brick stairwell. | 3.0s | **3.2s** | +0.2s |
| `act_knife_pull` | Aggression | `serial` | `0.00` | Character pulls out a Swiss Army switchblade, clicking the blade open in front of someone's face. | 2.1s | **2.3s** | +0.2s |
| `car_broadside_crash` | Vehicle | `serial` | `0.00` | Broadside T-bone car crash at an intersection at night, glass and metal debris. | 1.8s | **1.8s** | +0.0s |
| `cam_whip_pan` | Camera Movement | `serial` | `0.00` | Fast whip-pan left to right revealing a wooden desk. | 1.0s | **0.8s** | -0.2s |
| `cam_tracking_dolly` | Camera Movement | `serial` | `0.00` | Tracking dolly shot following a character walking down a hallway. | 2.5s | **2.3s** | -0.2s |
| `react_confused_stare` | Facial Reaction | `serial` | `0.00` | Glassy empty-eyed confused stare of an elderly person with dementia. | 2.0s | **2.1s** | +0.1s |
| `act_weightlifting` | Physical Action | `serial` | `0.00` | Man doing preacher curls with a 50lb barbell, then dropping the bar to the ground. | 2.5s | **2.6s** | +0.1s |
| `act_choke_wall` | Aggression | `serial` | `0.00` | Aggressive man grabs character by the neck, pinning them against a hallway wall. | 2.4s | **2.7s** | +0.3s |
