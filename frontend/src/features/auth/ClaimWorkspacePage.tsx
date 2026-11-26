import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError, NetworkError } from '../../api/http';
import type { DomainCheckResult, DomainClaim } from '../../api/types';
import { BreakableEmail } from '../../components/BreakableEmail';
import { Icon } from '../../components/Icon';
import { longDate, monogram } from '../../lib/format';
import { CheckOutcome, domainFailure, RecordSteps } from '../domain/DnsRecord';
import d from '../domain/Domain.module.css';
import styles from './Auth.module.css';
import { PublicLayout } from './PublicLayout';

/** What the accept page is told when it opens a claim's admin invitation */
export interface ClaimedState {
  claim: string;
}

/**
 * /claim-workspace/{token}, the link in signup's "already on Gratia" email (#61), for someone without an admin account
 * in their domain's workspace, typically IT. Adding their record to the domain proves they manage it: the workspace is
 * verified and they get an admin invitation (or their account there becomes an admin).
 */
export function ClaimWorkspacePage() {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const queryKey = ['domain-claim', token];
  const claim = useQuery({ queryKey, queryFn: ({ signal }) => api.domainClaim(token, signal), retry: false });
  const [result, setResult] = useState<DomainCheckResult | null>(null);
  const [failure, setFailure] = useState('');
  // The link expired between opening the page and checking
  const [gone, setGone] = useState<string | null>(null);

  const check = useMutation({
    mutationFn: () => api.checkDomainClaim(token),
    onMutate: () => setFailure(''),
    onSuccess: (r) => {
      if (r.status === 'Verified') void queryClient.invalidateQueries({ queryKey });
      if (r.status === 'Verified' && r.invitationToken) {
        const state: ClaimedState = { claim: r.message };
        navigate(`/accept-invitation/${encodeURIComponent(r.invitationToken)}`, { state });
        return;
      }
      setResult(r);
    },
    onError: (err) => {
      setResult(null);
      if (err instanceof ApiError && err.status === 404) return setGone(err.problem.detail ?? '');
      setFailure(domainFailure(err));
    },
  });

  const notFound = claim.error instanceof ApiError && claim.error.status === 404;
  let content;
  if (gone !== null || notFound) content = <DeadLink detail={gone || (claim.error instanceof ApiError ? claim.error.problem.detail : null)} />;
  else if (claim.isError)
    content = (
      <div role="alert" className={styles.card}>
        <h1 className={styles.cardTitle}>We couldn’t open this link</h1>
        <p className={styles.cardText}>
          {claim.error instanceof NetworkError ? 'You seem to be offline. Check your connection and try again.' : 'Something went wrong on our side. Try again in a moment.'}
        </p>
        <div className={styles.cardActions}>
          <button type="button" className="btn btn-primary" onClick={() => void claim.refetch()}>
            <Icon name="refresh" />
            Retry
          </button>
        </div>
      </div>
    );
  else if (!claim.data)
    content = (
      <p role="status" className={styles.lead}>
        Opening your link…
      </p>
    );
  // Verified, and the address already had an account there: it's an admin now
  else if (result?.status === 'Verified')
    content = (
      <div role="status" className={styles.card}>
        <span aria-hidden="true" className={styles.cardIcon}>
          <Icon name="how_to_reg" size={28} />
        </span>
        <h1 className={styles.cardTitle}>You’re an admin of {claim.data.companyName}</h1>
        <p className={styles.cardText}>{result.message}</p>
        <div className={styles.cardActions}>
          <Link to="/sign-in" state={{ email: claim.data.email }} className="btn btn-primary">
            Sign in
          </Link>
        </div>
      </div>
    );
  else
    content = (
      <ClaimForm
        claim={claim.data}
        result={result}
        failure={failure}
        checking={check.isPending}
        onCheck={() => check.mutate()}
      />
    );

  return (
    <PublicLayout>
      <div className={`${styles.form} ${d.claim}`}>{content}</div>
    </PublicLayout>
  );
}

function ClaimForm({
  claim,
  result,
  failure,
  checking,
  onCheck,
}: {
  claim: DomainClaim;
  result: DomainCheckResult | null;
  failure: string;
  checking: boolean;
  onCheck: () => void;
}) {
  const { companyName, domain } = claim;
  return (
    <>
      <div className={styles.company}>
        <span aria-hidden="true" className={styles.companyTile}>
          {monogram(companyName)}
        </span>
        <div>
          <div className={styles.companyName}>{companyName}</div>
          <div className={styles.companySub}>on Gratia</div>
        </div>
      </div>

      <div className={styles.intro}>
        <h1 className={styles.title}>Claim {companyName}</h1>
        <p className={styles.lead}>
          {companyName} already has a Gratia workspace for {domain}. If you manage {domain}’s DNS settings, add the record below to the domain to become
          one of its admins.
        </p>
      </div>

      <div className={styles.facts}>
        <div style={{ flex: '1 1 200px', minWidth: 0 }}>
          <div className={styles.factLabel}>Email</div>
          <div className={styles.factValue}>
            <Icon name="lock" size={16} className={styles.factIcon} />
            <span>
              <BreakableEmail email={claim.email} />
            </span>
          </div>
        </div>
        <div style={{ flex: '1 1 160px', minWidth: 0 }}>
          <div className={styles.factLabel}>Domain</div>
          <div className={styles.factValue}>
            <span>
              {domain}
              {claim.isVerified && (
                <span className={d.verified}>
                  <Icon name="verified" size={16} />
                  Verified
                </span>
              )}
            </span>
          </div>
        </div>
      </div>

      {claim.isUsed ? (
        // The record was found before: checking again only gives back the invitation it brought, while it's unused
        <div className={styles.intro}>
          <p className={styles.lead}>You’ve already claimed {companyName} with this link. Continue to finish joining it.</p>
          <div className={styles.cardActions}>
            <button type="button" className="btn btn-primary" disabled={checking} aria-busy={checking} onClick={onCheck}>
              {checking ? 'Checking…' : 'Continue'}
            </button>
          </div>
        </div>
      ) : (
        <RecordSteps record={claim.record} domain={domain} checking={checking} checked={!!result} onCheck={onCheck} />
      )}

      <div role="status">
        <CheckOutcome result={result} failure={failure}>
          {/sign in/i.test(failure) && (
            <>
              {' '}
              <Link to="/sign-in" state={{ email: claim.email }}>
                Go to sign in
              </Link>
            </>
          )}
        </CheckOutcome>
      </div>

      <p className={styles.hint}>
        The workspace’s admins are told by email. This link works until {longDate(claim.expiresAt)}.
      </p>
    </>
  );
}

function DeadLink({ detail }: { detail: string | null | undefined }) {
  return (
    <div role="alert" className={styles.card}>
      <span aria-hidden="true" className={styles.cardIcon}>
        <Icon name="link_off" size={28} />
      </span>
      <h1 className={styles.cardTitle}>This link doesn’t work any more</h1>
      <p className={styles.cardText}>{detail || 'Claim links work for 7 days. Sign up again with your work email to get a new one.'}</p>
      <div className={styles.cardActions}>
        <Link to="/create-workspace" className="btn btn-primary">
          Sign up again
        </Link>
        <Link to="/sign-in" className="btn btn-secondary">
          Go to sign in
        </Link>
      </div>
    </div>
  );
}
