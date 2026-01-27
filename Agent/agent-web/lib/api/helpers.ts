export function resolveJsonReferences(data: any): any {
  // Early return for non-objects
  if (!data || typeof data !== "object") {
    return data;
  }

  // Build a map of all objects with $id for reference resolution
  const idMap = new Map<string, any>();
  const buildIdMap = (obj: any, visited = new WeakSet()) => {
    if (!obj || typeof obj !== "object" || visited.has(obj)) {
      return;
    }
    visited.add(obj);

    if (Array.isArray(obj)) {
      obj.forEach((item) => buildIdMap(item, visited));
    } else {
      if (obj.$id) {
        idMap.set(obj.$id, obj);
      }
      Object.values(obj).forEach((value) => buildIdMap(value, visited));
    }
  };
  buildIdMap(data);

  // Resolve references and clean up metadata
  const resolve = (obj: any, visited = new WeakSet()): any => {
    if (!obj || typeof obj !== "object") {
      return obj;
    }

    if (visited.has(obj)) {
      return obj;
    }
    visited.add(obj);

    // If this is a pure reference object (only has $ref), try to resolve it
    if (obj.$ref && Object.keys(obj).length === 1) {
      const resolved = idMap.get(obj.$ref);
      if (resolved) {
        return resolve(resolved, visited);
      }
      // If we can't resolve it, return null to filter it out
      return null;
    }

    // If it's an array, process each item
    if (Array.isArray(obj)) {
      return obj
        .map((item) => resolve(item, visited))
        .filter((item) => item !== null && item !== undefined);
    }

    // Process object properties
    const result: any = {};
    for (const [key, value] of Object.entries(obj)) {
      // Skip $id and $ref metadata properties
      if (key === "$id" || key === "$ref") {
        continue;
      }
      result[key] = resolve(value, visited);
    }
    return result;
  };

  const resolved = resolve(data);

  // Final filter for arrays: remove objects that don't have required project fields
  if (Array.isArray(resolved)) {
    return resolved.filter((item) => {
      if (!item || typeof item !== "object") {
        return false;
      }
      // Keep objects that have actual project data
      return (
        item.id !== undefined ||
        item.submissionProjectId !== undefined ||
        item.submissionProjectName !== undefined
      );
    });
  }

  return resolved;
}



// Helper to check if error is a Next.js redirect
// Checks for the NEXT_REDIRECT digest property
export function isNextRedirectError(error: unknown): boolean {
    return (error as any)?.digest?.startsWith?.("NEXT_REDIRECT") ?? false;
  }
  
  