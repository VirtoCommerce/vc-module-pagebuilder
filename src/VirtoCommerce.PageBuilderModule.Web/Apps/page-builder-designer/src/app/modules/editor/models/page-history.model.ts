/**
 * A version of the open page: one commit that touched its file, wherever in the content repository it
 * lives. Only stores whose pages are kept in git have these — the server offers the `history` descriptor
 * for those and withholds it for the rest, which is what hides the panel entirely.
 */
export interface PageVersion {
    sha: string;
    shortSha: string;
    date?: string;
    message?: string;
    author?: {
        name?: string;
        email?: string;
        /** GitHub account the author's email resolves to, when it resolves to one. */
        login?: string;
    };
    /** Platform login recorded by the server; absent for edits made outside the builder. */
    vcUser?: string;
    /** Branches the commit is reachable from — a version can sit on several at once. */
    branches: string[];
    /** The production branch has this commit: it is what visitors see. */
    published: boolean;
    /** This version is on my own work branch. */
    mine: boolean;
    /** A commit that changed far more than this page — an import or a bulk reformat. */
    bulk: boolean;
    changedFiles?: number;
}

export interface PageHistory {
    versions: PageVersion[];
    /** More branches exist than the server looked at, so the list may be missing unpublished versions. */
    truncated: boolean;
    endCursor?: string;
    /** Unpublished versions that are neither mine nor bulk — the number on the toolbar badge. */
    otherDraftCount: number;
    myDraftBranch?: string;
}

/** Per-page history as the store holds it, including what is going on with it right now. */
export interface PageHistoryState extends PageHistory {
    isLoading: boolean;
    error?: string;
    /** Sha being restored, so the row that was clicked can say so. */
    restoring?: string;
}

/**
 * Consecutive versions by the same author on the same branch, folded into one row.
 *
 * Saving is a commit, so an afternoon of editing is a stack of near-identical entries — twelve of them
 * within three hours on one page in the content repository. The newest carries the group; the rest are
 * there to be expanded.
 */
export interface PageVersionGroup {
    version: PageVersion;
    older: PageVersion[];
}
