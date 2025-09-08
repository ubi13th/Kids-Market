"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.sendNotification = void 0;
const functions = require("firebase-functions");
const admin = require("firebase-admin");
if (!admin.apps.length) {
    admin.initializeApp();
}
const TOKENS_PATH = "/DeviceTokens";
function makeTitleBody(p) {
    const map = {
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
function extractTokensAndKeys(val) {
    const tokens = [];
    const tokenToKey = {};
    if (!val || typeof val !== "object")
        return { tokens, tokenToKey };
    for (const [key, v] of Object.entries(val)) {
        if (v && typeof v === "object" && typeof v.token === "string") {
            const token = String(v.token);
            tokens.push(token);
            tokenToKey[token] = key;
            continue;
        }
        if (v === true && key && key.length > 50) {
            const token = String(key);
            tokens.push(token);
            tokenToKey[token] = key;
            continue;
        }
    }
    return { tokens, tokenToKey };
}
exports.sendNotification = functions.https.onCall(async (data, context) => {
    if (!context.auth) {
        throw new functions.https.HttpsError("unauthenticated", "Auth required.");
    }
    const { targetUid, type } = data || {};
    if (!targetUid || !type) {
        throw new functions.https.HttpsError("invalid-argument", "targetUid and type are required.");
    }
    const snap = await admin.database().ref(`${TOKENS_PATH}/${targetUid}`).get();
    if (!snap.exists()) {
        return { sent: 0, failed: 0, pruned: 0, message: "No tokens for target user." };
    }
    const { tokens, tokenToKey } = extractTokensAndKeys(snap.val());
    if (!tokens.length) {
        return { sent: 0, failed: 0, pruned: 0, message: "No valid tokens for target user." };
    }
    const { title, body } = makeTitleBody(data);
    const message = {
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
    const toDelete = {};
    resp.responses.forEach((r, idx) => {
        if (!r.success) {
            const code = r.error?.code || "";
            if (code === "messaging/registration-token-not-registered" ||
                code === "messaging/invalid-registration-token") {
                const tok = tokens[idx];
                const key = tokenToKey[tok];
                if (key)
                    toDelete[`${TOKENS_PATH}/${targetUid}/${key}`] = null;
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
//# sourceMappingURL=index.js.map