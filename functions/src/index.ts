// Gen-1 Cloud Functions (firebase-functions v4.x)
import * as functions from "firebase-functions";
import * as admin from "firebase-admin";

if (!admin.apps.length) admin.initializeApp();

/** -------------- Types ------------------ */
type EventType =
    | "ContractCreated"
    | "ContractSubmittedByChild"
    | "ContractApprovedByAdmin"
    | "ContractDeclinedByAdmin"
    | "ContractPurchasedByAdmin"
    | "ContractUndo"
    | "SurpriseContractCreated"
    | "SurpriseContractUpdated";

interface Payload {
    targetUid: string;
    type: EventType;
    actorUid?: string;
    actorRole?: string;
    contractId?: string;
    contractTitle?: string;
    amount?: number;
    isSurprise?: boolean;
}

/** -------------- Constants -------------- */
// MUST MATCH Unity channel id below.
const ANDROID_CHANNEL_ID = "kids_market_default";
const TOKENS_PATH = "/DeviceTokens";

/** -------------- Helpers ---------------- */
function makeTitleBody(p: Payload): { title: string; body: string } {
    const map: Record<EventType, string> = {
        ContractCreated: "New contract",
        ContractSubmittedByChild: "Submission ready",
        ContractApprovedByAdmin: "Approved 🎉",
        ContractDeclinedByAdmin: "Declined",
        ContractPurchasedByAdmin: "Purchased",
        ContractUndo: "Action undone",
        SurpriseContractCreated: "New surprise contract",
        SurpriseContractUpdated: "Surprise contract updated",
    };
    const title = map[p.type] ?? "Update";
    const name = p.contractTitle ?? "a contract";
    const amt = typeof p.amount === "number" ? ` (+${p.amount})` : "";

    switch (p.type) {
        case "ContractCreated": return { title, body: `A new contract “${name}” is available.${amt}` };
        case "ContractSubmittedByChild": return { title, body: `“${name}” was submitted and is ready to review.${amt}` };
        case "ContractApprovedByAdmin": return { title, body: `“${name}” was approved!${amt}` };
        case "ContractDeclinedByAdmin": return { title, body: `“${name}” was declined.` };
        case "ContractPurchasedByAdmin": return { title, body: `“${name}” was purchased.${amt}` };
        case "ContractUndo": return { title, body: `An action on “${name}” was undone.` };
        case "SurpriseContractCreated": return { title, body: `New surprise contract “${name}” awaiting review.` };
        case "SurpriseContractUpdated": return { title, body: `Surprise contract “${name}” was updated.` };
        default: return { title: "Update", body: "There's an update." };
    }
}

// Supports both schemas:
//  A) /DeviceTokens/<uid>/<encodedKey> : { token, ts }
//  B) /DeviceTokens/<uid>/<rawToken>   : true
function extractTokensAndKeys(val: any): { tokens: string[]; tokenToKey: Record<string, string> } {
    const tokens: string[] = [];
    const tokenToKey: Record<string, string> = {};
    if (!val || typeof val !== "object") return { tokens, tokenToKey };

    for (const [key, v] of Object.entries(val)) {
        if (v && typeof v === "object" && typeof (v as any).token === "string") {
            const token = String((v as any).token);
            tokens.push(token);
            tokenToKey[token] = key; // encoded key
            continue;
        }
        if (v === true && key && key.length > 50) {
            const token = String(key);
            tokens.push(token);
            tokenToKey[token] = key; // raw token
            continue;
        }
    }
    return { tokens, tokenToKey };
}

/** -------------- Callable ---------------- */
export const sendNotification = functions
    .region("us-central1")
    .https.onCall(async (data: Payload, context) => {
        if (!context.auth) {
            throw new functions.https.HttpsError("unauthenticated", "Auth required.");
        }
        const { targetUid, type } = data || ({} as Payload);
        if (!targetUid || !type) {
            throw new functions.https.HttpsError("invalid-argument", "targetUid and type are required.");
        }

        // Load tokens
        const snap = await admin.database().ref(`${TOKENS_PATH}/${targetUid}`).get();
        if (!snap.exists()) {
            return { sent: 0, failed: 0, pruned: 0, message: "No tokens for target user." };
        }
        const { tokens, tokenToKey } = extractTokensAndKeys(snap.val());
        if (!tokens.length) {
            return { sent: 0, failed: 0, pruned: 0, message: "No valid tokens for target user." };
        }

        const { title, body } = makeTitleBody(data);

        const msg: admin.messaging.MulticastMessage = {
            tokens,
            notification: { title, body },   // good defaults for most platforms
            data: {
                type: data.type,
                actorUid: data.actorUid || "",
                actorRole: data.actorRole || "",
                contractId: data.contractId || "",
                contractTitle: data.contractTitle || "",
                amount: data.amount?.toString() || "",
                isSurprise: data.isSurprise ? "true" : "false",
            },
            android: {
                priority: "high",
                notification: {
                    channelId: ANDROID_CHANNEL_ID, // MUST MATCH Unity
                    sound: "default",
                    visibility: "public",
                    defaultVibrateTimings: true,
                    defaultSound: true,
                },
            },
            apns: {
                headers: { "apns-priority": "10" },
                payload: {
                    aps: {
                        alert: { title, body },     // ensures banners on iOS
                        sound: "default",
                        badge: 1,
                    },
                },
            },
        };

        const resp = await admin.messaging().sendEachForMulticast(msg);

        // Prune invalid tokens
        const toDelete: Record<string, null> = {};
        resp.responses.forEach((r, idx) => {
            if (!r.success) {
                const code = r.error?.code || "";
                if (
                    code === "messaging/registration-token-not-registered" ||
                    code === "messaging/invalid-registration-token"
                ) {
                    const tok = tokens[idx];
                    const key = tokenToKey[tok];
                    if (key) toDelete[`${TOKENS_PATH}/${targetUid}/${key}`] = null;
                }
            }
        });

        let pruned = 0;
        if (Object.keys(toDelete).length) {
            await admin.database().ref().update(toDelete);
            pruned = Object.keys(toDelete).length;
        }

        return {
            sent: resp.successCount,
            failed: resp.failureCount,
            pruned,
        };
    });










// Gen-1 Cloud Functions (firebase-functions v4.x)
/*import * as functions from "firebase-functions";
import * as admin from "firebase-admin";

if (!admin.apps.length) {
    admin.initializeApp();
}

type EventType =
    | "ContractCreated"
    | "ContractSubmittedByChild"
    | "ContractApprovedByAdmin"
    | "ContractDeclinedByAdmin"
    | "ContractPurchasedByAdmin"
    | "ContractUndo"
    | "SurpriseContractCreated"
    | "SurpriseContractUpdated";

interface Payload {
    targetUid: string;
    type: EventType;
    actorUid?: string;
    actorRole?: string;
    contractId?: string;
    contractTitle?: string;
    amount?: number;
    isSurprise?: boolean;
}

const TOKENS_PATH = "/DeviceTokens";

// Build a friendly title/body per event
function makeTitleBody(p: Payload): { title: string; body: string } {
    const map: Record<EventType, string> = {
        ContractCreated: "New contract",
        ContractSubmittedByChild: "Submission ready",
        ContractApprovedByAdmin: "Approved 🎉",
        ContractDeclinedByAdmin: "Declined",
        ContractPurchasedByAdmin: "Purchased",
        ContractUndo: "Action undone",
        SurpriseContractCreated: "New surprise contract",
        SurpriseContractUpdated: "Surprise contract updated",
    };
    const title = map[p.type] ?? "Update";

    const name = p.contractTitle ?? "a contract";
    const amt = typeof p.amount === "number" ? ` (+${p.amount})` : "";

    switch (p.type) {
        case "ContractCreated": return { title, body: `A new contract “${name}” is available.${amt}` };
        case "ContractSubmittedByChild": return { title, body: `“${name}” was submitted and is ready to review.${amt}` };
        case "ContractApprovedByAdmin": return { title, body: `“${name}” was approved!${amt}` };
        case "ContractDeclinedByAdmin": return { title, body: `“${name}” was declined.` };
        case "ContractPurchasedByAdmin": return { title, body: `“${name}” was purchased.${amt}` };
        case "ContractUndo": return { title, body: `An action on “${name}” was undone.` };
        case "SurpriseContractCreated": return { title, body: `New surprise contract “${name}” awaiting review.` };
        case "SurpriseContractUpdated": return { title, body: `Surprise contract “${name}” was updated.` };
        default: return { title: "Update", body: "There's an update." };
    }
}

// Extract tokens from RTDB, supporting both:
//  A) /DeviceTokens/<uid>/<encodedKey> : { token, ts }
//  B) /DeviceTokens/<uid>/<rawToken>   : true
function extractTokensAndKeys(val: any): { tokens: string[]; tokenToKey: Record<string, string> } {
    const tokens: string[] = [];
    const tokenToKey: Record<string, string> = {};

    if (!val || typeof val !== "object") return { tokens, tokenToKey };

    for (const [key, v] of Object.entries(val)) {
        // New schema: child is an object with { token, ts }
        if (v && typeof v === "object" && typeof (v as any).token === "string") {
            const token = String((v as any).token);
            tokens.push(token);
            tokenToKey[token] = key; // key is our base64url of the token
            continue;
        }

        // Legacy schema: child key is the token string, value true
        if (v === true && key && key.length > 50) { // FCM tokens are long
            const token = String(key);
            tokens.push(token);
            tokenToKey[token] = key; // key equals the token itself
            continue;
        }

        // Ignore anything else (e.g., stray "StubToken": true)
    }

    return { tokens, tokenToKey };
}

export const sendNotification = functions.https.onCall(async (data: Payload, context) => {
    // Require auth
    if (!context.auth) {
        throw new functions.https.HttpsError("unauthenticated", "Auth required.");
    }

    const { targetUid, type } = data || ({} as Payload);
    if (!targetUid || !type) {
        throw new functions.https.HttpsError("invalid-argument", "targetUid and type are required.");
    }

    // Load tokens
    const snap = await admin.database().ref(`${TOKENS_PATH}/${targetUid}`).get();
    if (!snap.exists()) {
        return { sent: 0, failed: 0, pruned: 0, message: "No tokens for target user." };
    }

    const { tokens, tokenToKey } = extractTokensAndKeys(snap.val());
    if (!tokens.length) {
        return { sent: 0, failed: 0, pruned: 0, message: "No valid tokens for target user." };
    }

    const { title, body } = makeTitleBody(data);

    const message: admin.messaging.MulticastMessage = {
        tokens,
        notification: { title, body },
        data: {
            type: data.type,
            actorUid: data.actorUid || "",
            actorRole: data.actorRole || "",
            contractId: data.contractId || "",
            contractTitle: data.contractTitle || "",
            amount: data.amount?.toString() || "",
            isSurprise: data.isSurprise ? "true" : "false",
        },
        android: {
            priority: "high",
            notification: { channelId: "kids_market_default", sound: "default" },
        },
        apns: {
            headers: { "apns-priority": "10" },
            payload: { aps: { sound: "default", contentAvailable: false } },
        },
    };

    const resp = await admin.messaging().sendEachForMulticast(message);

    // Prune invalid tokens from either schema
    const toDelete: Record<string, null> = {};
    resp.responses.forEach((r, idx) => {
        if (!r.success) {
            const code = r.error?.code || "";
            if (
                code === "messaging/registration-token-not-registered" ||
                code === "messaging/invalid-registration-token"
            ) {
                const tok = tokens[idx];
                const key = tokenToKey[tok]; // either base64url key (new) or raw token (legacy)
                if (key) toDelete[`${TOKENS_PATH}/${targetUid}/${key}`] = null;
            }
        }
    });

    let pruned = 0;
    if (Object.keys(toDelete).length) {
        await admin.database().ref().update(toDelete);
        pruned = Object.keys(toDelete).length;
    }

    return {
        sent: resp.successCount,
        failed: resp.failureCount,
        pruned,
    };
});
*/
