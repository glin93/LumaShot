# Local modification

Upstream: OpenAI Sign in with ChatGPT DevKit, commit `0a36fefeb913055c8c7a1a29b63d82b96b2e841a`.

LumaShot changes `src/responses.ts` and `src/types.ts` to accept strictly validated inline PNG/JPEG image parts on user messages. OAuth, identity verification, encrypted credential rotation, account selection and stream completion handling remain upstream implementations.

These modifications are distributed under the same Sign-in with ChatGPT DevKit Noncommercial License in `LICENSE`. Personal experimentation/development only; commercial use of the DevKit requires a separate agreement with OpenAI. This is a source-available dependency, not an OSI open-source license.
