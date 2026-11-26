import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router';
import { api } from '../../api/endpoints';
import { ApiError } from '../../api/http';
import { queryKeys, useCompany, useDomainVerification } from '../../api/queries';
import type { DomainCheckResult } from '../../api/types';
import { Icon } from '../../components/Icon';
import { longDate } from '../../lib/format';
import { CheckOutcome, domainFailure, RecordSteps } from './DnsRecord';
import s from './Domain.module.css';

/**
 * Domain verification (#61), for every member of a workspace with a domain. Admins find it in Company settings, where
 * it verifies the domain: no more domain rule, no daily cap on invitation emails. Everyone else finds it in My profile,
 * where it's how whoever manages the domain's DNS (usually IT) becomes an admin. Each person has their own record.
 */
export function DomainVerificationCard({ place }: { place: 'settings' | 'profile' }) {
  const query = useDomainVerification();
  const { data: company } = useCompany();
  const queryClient = useQueryClient();
  const location = useLocation();
  const ref = useRef<HTMLElement>(null);
  const [result, setResult] = useState<DomainCheckResult | null>(null);
  const [failure, setFailure] = useState('');
  const dv = query.data;

  // /admin/settings#domain, from the invite form's domain rule and daily cap
  const loaded = !!dv;
  useEffect(() => {
    if (loaded && location.hash === '#domain') ref.current?.scrollIntoView({ block: 'start' });
  }, [loaded, location.hash]);

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.domainVerification });
    void queryClient.invalidateQueries({ queryKey: queryKeys.company });
  };
  const start = useMutation({
    mutationFn: api.startDomainVerification,
    onMutate: () => {
      setFailure('');
      setResult(null);
    },
    onSuccess: (next) => queryClient.setQueryData(queryKeys.domainVerification, next),
    onError: (err) => {
      // 409: another admin verified it meanwhile ("… is already verified."). The card is out of date either way.
      if (err instanceof ApiError && (err.status === 409 || err.status === 400)) refresh();
      setFailure(err instanceof ApiError && err.status === 409 && err.problem.detail ? err.problem.detail : domainFailure(err));
    },
  });
  const check = useMutation({
    mutationFn: api.checkDomainVerification,
    onMutate: () => setFailure(''),
    onSuccess: (r) => {
      setResult(r);
      if (r.status !== 'Verified') return;
      refresh();
      // Someone who wasn't an admin is one now. Their next request gets a 401 and the refresh brings the role (#73),
      // so refetching the people list (where "me" comes from) updates the navigation.
      void queryClient.invalidateQueries({ queryKey: queryKeys.users });
    },
    onError: (err) => {
      setResult(null);
      // 400 "Get your record first…": it was used, or an admin changed this member's email, which deletes it
      if (err instanceof ApiError && err.status === 400) void queryClient.invalidateQueries({ queryKey: queryKeys.domainVerification });
      setFailure(domainFailure(err));
    },
  });

  if (query.isError)
    return (
      <section className={s.card} aria-label="Domain verification">
        <p className={s.text}>We couldn’t load your company’s domain verification.</p>
        <div className={s.actions}>
          <button type="button" className="btn btn-secondary" onClick={() => void query.refetch()}>
            <Icon name="refresh" />
            Retry
          </button>
        </div>
      </section>
    );
  // A workspace founded with a personal address has no domain to verify
  if (!dv?.domain) return null;
  const verifiedNow = result?.status === 'Verified';
  // Admins verify from Company settings. One who just became an admin here still sees how it went.
  if (place === 'profile' && dv.isAdmin && !verifiedNow) return null;

  const domain = dv.domain;
  const companyName = company?.name ?? 'your company';
  const showVerified = verifiedNow || (dv.isAdmin && dv.isVerified);
  const title = showVerified ? `${domain} is verified` : dv.isAdmin ? `Verify ${domain}` : `Manage ${domain}’s DNS?`;

  return (
    <section ref={ref} id="domain" className={s.card} aria-labelledby={`domain-${place}-title`}>
      <div className={s.head}>
        <span aria-hidden="true" className={s.headIcon} data-tone={showVerified ? 'ok' : undefined}>
          <Icon name={showVerified ? 'verified' : 'domain_verification'} size={22} />
        </span>
        <div className={s.headText}>
          <h2 id={`domain-${place}-title`} className={s.title}>
            {title}
          </h2>
          {showVerified ? (
            !verifiedNow && (
              <p className={s.text}>
                {dv.verifiedAt ? `Verified on ${longDate(dv.verifiedAt)}. ` : ''}Admins can invite people on any domain, with no daily limit on
                invitation emails.
              </p>
            )
          ) : dv.isAdmin ? (
            <p className={s.text}>
              Until it’s verified, admins can only invite @{domain} addresses, and only a limited number of invitation emails go out each day. To verify
              it, add a record we give you to {domain}’s DNS settings. If someone else manages them, usually IT, send them the record.
            </p>
          ) : (
            <p className={s.text}>
              {dv.isVerified ? 'Add' : 'Verify it: add'} a record we give you to its DNS settings, and you become an admin of {companyName}. It’s how
              IT gets in when someone else set the workspace up. The admins are told by email.
            </p>
          )}
        </div>
      </div>

      {!showVerified &&
        (dv.record ? (
          <RecordSteps record={dv.record} domain={domain} checking={check.isPending} checked={!!result} onCheck={() => check.mutate()} />
        ) : (
          <div className={s.actions}>
            <button
              type="button"
              className={`btn ${dv.isAdmin ? 'btn-primary' : 'btn-secondary'}`}
              disabled={start.isPending}
              aria-busy={start.isPending}
              onClick={() => start.mutate()}
            >
              {start.isPending ? 'Getting your record…' : 'Get your DNS record'}
            </button>
          </div>
        ))}

      {/* One live region for the whole card, so what a check found is read out even when the card changes with it */}
      <div role="status" className={s.outcome}>
        <CheckOutcome result={result} failure={failure} />
        {verifiedNow && place === 'profile' && dv.isAdmin && (
          <p className={s.next}>
            <Link to="/admin/settings" className={s.link}>
              Open Company settings
            </Link>
          </p>
        )}
      </div>
    </section>
  );
}
